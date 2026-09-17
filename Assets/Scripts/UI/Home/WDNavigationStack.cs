using System;
using System.Collections.Generic;

public sealed class WDNavigationContext
{
    public string Id { get; }
    public Func<object> CaptureState { get; }
    public Action<object> Restore { get; }
    public Action Exit { get; }
    public Action<object> DiscardState { get; }

    public WDNavigationContext(
        string id, Action<object> restore,
        Func<object> captureState = null, Action exit = null,
        Action<object> discardState = null)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("O contexto de navegação precisa de um identificador.", nameof(id));

        Id = id;
        Restore = restore ?? throw new ArgumentNullException(nameof(restore));
        CaptureState = captureState;
        Exit = exit;
        DiscardState = discardState;
    }
}

public sealed class WDNavigationStack
{
    private sealed class Entry
    {
        public WDNavigationContext Context { get; }
        public object State { get; set; }

        public Entry(WDNavigationContext context) => Context = context;
    }

    private readonly List<Entry> entries = new();

    public int Count => entries.Count;
    public bool CanGoBack => entries.Count > 1;
    public string CurrentId => entries.Count == 0 ? string.Empty : entries[^1].Context.Id;

    public void SetRoot(WDNavigationContext root)
    {
        if (root == null)
            throw new ArgumentNullException(nameof(root));

        DiscardAllEntries();
        entries.Add(new Entry(root));
        root.Restore(null);
    }

    public void Push(WDNavigationContext context)
    {
        if (context == null)
            throw new ArgumentNullException(nameof(context));
        if (entries.Count == 0)
            throw new InvalidOperationException("Defina a raiz antes de navegar.");

        Capture(entries[^1]);
        entries.Add(new Entry(context));
        context.Restore(null);
    }

    public bool TryBack()
    {
        if (!CanGoBack)
            return false;

        Entry current = entries[^1];
        Capture(current);
        entries.RemoveAt(entries.Count - 1);
        current.Context.Exit?.Invoke();
        current.Context.DiscardState?.Invoke(current.State);

        Entry previous = entries[^1];
        previous.Context.Restore(previous.State);
        return true;
    }

    public void ResetToRoot()
    {
        if (entries.Count == 0)
            return;

        while (entries.Count > 1)
        {
            Entry current = entries[^1];
            Capture(current);
            entries.RemoveAt(entries.Count - 1);
            current.Context.Exit?.Invoke();
            current.Context.DiscardState?.Invoke(current.State);
        }

        Entry root = entries[0];
        root.Context.Restore(root.State);
    }

    private static void Capture(Entry entry)
    {
        if (entry.Context.CaptureState != null)
            entry.State = entry.Context.CaptureState();
    }

    private void DiscardAllEntries()
    {
        for (int i = entries.Count - 1; i >= 0; i--)
        {
            Entry entry = entries[i];
            Capture(entry);
            entry.Context.Exit?.Invoke();
            entry.Context.DiscardState?.Invoke(entry.State);
        }
        entries.Clear();
    }
}

public sealed class WDNavigationBackRouter
{
    private readonly WDNavigationStack navigation;
    private readonly Func<bool> tryHandleCancelableContext;
    private int lastCommand = int.MinValue;
    private bool hasLastCommand;

    public WDNavigationBackRouter(
        WDNavigationStack navigation,
        Func<bool> tryHandleCancelableContext)
    {
        this.navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
        this.tryHandleCancelableContext = tryHandleCancelableContext;
    }

    public bool TryHandleBack(int command)
    {
        if (hasLastCommand && command == lastCommand)
            return false;

        hasLastCommand = true;
        lastCommand = command;
        if (tryHandleCancelableContext?.Invoke() == true)
            return true;

        return navigation.TryBack();
    }
}

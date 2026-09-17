using System.Collections.Generic;
using NUnit.Framework;

public sealed class WDNavigationStackTests
{
    [Test]
    public void PushCreatesANewCurrentContext()
    {
        var restored = new List<string>();
        WDNavigationStack stack = CreateStack(restored);

        stack.Push(Context("profile", restored));

        Assert.That(stack.Count, Is.EqualTo(2));
        Assert.That(stack.CurrentId, Is.EqualTo("profile"));
        Assert.That(restored, Is.EqualTo(new[] { "home", "profile" }));
    }

    [Test]
    public void BackRestoresExactlyThePreviousEntry()
    {
        var restored = new List<string>();
        WDNavigationStack stack = CreateStack(restored);
        stack.Push(Context("profile", restored));
        stack.Push(Context("chat", restored));

        bool handled = stack.TryBack();

        Assert.That(handled, Is.True);
        Assert.That(stack.CurrentId, Is.EqualTo("profile"));
        Assert.That(restored[^1], Is.EqualTo("profile"));
    }

    [Test]
    public void RootIsProtectedAndCannotBePopped()
    {
        var restored = new List<string>();
        WDNavigationStack stack = CreateStack(restored);

        Assert.That(stack.TryBack(), Is.False);
        Assert.That(stack.Count, Is.EqualTo(1));
        Assert.That(stack.CurrentId, Is.EqualTo("home"));
    }

    [Test]
    public void NestedSequenceUnwindsInReverseOrderUntilHome()
    {
        var restored = new List<string>();
        WDNavigationStack stack = CreateStack(restored);
        stack.Push(Context("a", restored));
        stack.Push(Context("b", restored));
        stack.Push(Context("c", restored));

        Assert.That(stack.TryBack(), Is.True);
        Assert.That(stack.CurrentId, Is.EqualTo("b"));
        Assert.That(stack.TryBack(), Is.True);
        Assert.That(stack.CurrentId, Is.EqualTo("a"));
        Assert.That(stack.TryBack(), Is.True);
        Assert.That(stack.CurrentId, Is.EqualTo("home"));
        Assert.That(stack.TryBack(), Is.False);
    }

    [Test]
    public void EquivalentDestinationsRemainDistinctEntries()
    {
        var restored = new List<string>();
        WDNavigationStack stack = CreateStack(restored);
        stack.Push(Context("profile", restored));
        stack.Push(Context("chat", restored));
        stack.Push(Context("profile", restored));

        Assert.That(stack.Count, Is.EqualTo(4));
        Assert.That(stack.TryBack(), Is.True);
        Assert.That(stack.CurrentId, Is.EqualTo("chat"));
        Assert.That(stack.TryBack(), Is.True);
        Assert.That(stack.CurrentId, Is.EqualTo("profile"));
    }

    [Test]
    public void ContextStateIsCapturedAndRestoredWhenReturning()
    {
        int visibleValue = 7;
        int restoredValue = -1;
        var stack = new WDNavigationStack();
        stack.SetRoot(new WDNavigationContext(
            "home", state =>
            {
                if (state is int value)
                    restoredValue = value;
            },
            () => visibleValue));

        stack.Push(new WDNavigationContext("profile", _ => { }));
        visibleValue = 99;
        stack.TryBack();

        Assert.That(restoredValue, Is.EqualTo(7));
    }

    [Test]
    public void PoppedEntryDiscardsItsStateAndANewEntryStartsClean()
    {
        object firstRestoreState = new object();
        object secondRestoreState = new object();
        object discardedState = null;
        int restoreCount = 0;
        var stack = new WDNavigationStack();
        stack.SetRoot(new WDNavigationContext("home", _ => { }));

        WDNavigationContext CreateProfile() => new(
            "profile",
            state =>
            {
                restoreCount++;
                if (restoreCount == 1)
                    firstRestoreState = state;
                else
                    secondRestoreState = state;
            },
            () => "entry-state",
            discardState: state => discardedState = state);

        stack.Push(CreateProfile());
        stack.TryBack();
        stack.Push(CreateProfile());

        Assert.That(firstRestoreState, Is.Null);
        Assert.That(secondRestoreState, Is.Null);
        Assert.That(discardedState, Is.EqualTo("entry-state"));
    }

    [Test]
    public void SameBackCommandCannotPopTwice()
    {
        var restored = new List<string>();
        WDNavigationStack stack = CreateStack(restored);
        stack.Push(Context("a", restored));
        stack.Push(Context("b", restored));
        var router = new WDNavigationBackRouter(stack, null);

        Assert.That(router.TryHandleBack(42), Is.True);
        Assert.That(router.TryHandleBack(42), Is.False);
        Assert.That(stack.CurrentId, Is.EqualTo("a"));
        Assert.That(stack.Count, Is.EqualTo(2));
    }

    [Test]
    public void CancelableContextConsumesBackBeforeNavigationStack()
    {
        var restored = new List<string>();
        WDNavigationStack stack = CreateStack(restored);
        stack.Push(Context("profile", restored));
        bool modalOpen = true;
        var router = new WDNavigationBackRouter(stack, () =>
        {
            if (!modalOpen)
                return false;
            modalOpen = false;
            return true;
        });

        Assert.That(router.TryHandleBack(1), Is.True);
        Assert.That(stack.CurrentId, Is.EqualTo("profile"));
        Assert.That(router.TryHandleBack(2), Is.True);
        Assert.That(stack.CurrentId, Is.EqualTo("home"));
    }

    [Test]
    public void ExplicitResetDiscardsNestedEntriesAndRestoresRoot()
    {
        var restored = new List<string>();
        var discarded = new List<string>();
        WDNavigationStack stack = CreateStack(restored);
        stack.Push(new WDNavigationContext(
            "a", _ => restored.Add("a"), () => "a-state",
            discardState: _ => discarded.Add("a")));
        stack.Push(new WDNavigationContext(
            "b", _ => restored.Add("b"), () => "b-state",
            discardState: _ => discarded.Add("b")));

        stack.ResetToRoot();

        Assert.That(stack.Count, Is.EqualTo(1));
        Assert.That(stack.CurrentId, Is.EqualTo("home"));
        Assert.That(discarded, Is.EqualTo(new[] { "b", "a" }));
        Assert.That(restored[^1], Is.EqualTo("home"));
    }

    private static WDNavigationStack CreateStack(List<string> restored)
    {
        var stack = new WDNavigationStack();
        stack.SetRoot(Context("home", restored));
        return stack;
    }

    private static WDNavigationContext Context(string id, List<string> restored) =>
        new(id, _ => restored.Add(id));
}

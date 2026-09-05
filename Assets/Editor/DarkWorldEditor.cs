#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class DarkWorldEditor
{
    const string Root="Assets/Mapas/Dark World", Masks=Root+"/Mascaras", Art=Root+"/Arte/DarkWorld_Remaster.png", Guide=Root+"/Arte/DarkWorld_Remaster_Gabarito.png";
    const string ScenePath=Root+"/DarkWorld.unity", DefinitionPath="Assets/Resources/Mapas/DarkWorld.asset", CatalogPath="Assets/Resources/Mapas/CatalogoMapas.asset", BaseScene="Assets/WarGame.unity";
    const float Ppu=100f, Width=1529f, Height=1019f;
    readonly struct TCfg { public TCfg(string id,string name,string region,float x,float y,float tx,float ty){Id=id;Name=name;Region=region;AnchorPixel=new Vector2(x,y);CounterPixel=new Vector2(tx,ty);} public readonly string Id,Name,Region;public readonly Vector2 AnchorPixel,CounterPixel; }
    sealed class Geometry { public TCfg Territory; public Vector2 Position; public Vector2 Counter; public RectInt Bounds; public int Area; }
    static TCfg T(string i,string n,string r,float x,float y,float tx,float ty)=>new TCfg(i,n,r,x,y,tx,ty);
    static readonly TCfg[] Territories={
      T("Lai","Lai","agla",1029,177.5f,1027.3f,177.1f),T("Hevll","Hevll","agla",924,191.5f,922.7f,186.4f),T("SodherNoor","Sodher Noor","flathia",259.5f,202.5f,271.6f,194.9f),T("ValeriaHeart","Valeria Heart","agla",1123,204.5f,1122.7f,201.5f),
      T("SpearOfTheGods","Spear of the gods","flathia",441.5f,224.5f,426.6f,216.4f),T("Talisporto","Talisporto","agla",1009,236.5f,1011.2f,238),T("WhiteWall","White Wall","vorlia",703,283,730.1f,280.8f),T("Marcias","Marcias","akani",1267.5f,294,1279.2f,299),
      T("FlathiaIsland","Flathia Island","flathia",144,321,143.7f,321.6f),T("Cube","Cube","flathia",276.5f,348,270.8f,352.3f),T("CrowsHome","Crows Home","flathia",420,372,416.6f,364.8f),T("Babushka","Babushka","vorlia",805.5f,374,805.5f,375.7f),
      T("FantasyRoom","Fantasy Room","vorlia",669.5f,378,663.8f,384.7f),T("Nai","Nai","akani",1134.5f,396,1141.4f,393.1f),T("PeachLand","Peach Land","akani",1341.5f,424.5f,1340,416.9f),T("Tigershitman","Tigershitman","vorlia",902.5f,412.5f,905.6f,423.9f),
      T("PalmTowner","Palm Towner","flathia",260.5f,491,257,488.1f),T("NewSahara","New Sahara","flathia",365.5f,503,363.8f,504.8f),T("PutfireDistrict","Putfire District","akani",1254,549.5f,1261,545),T("Widows","Widows","birfani",607.5f,563.5f,610,557.6f),
      T("Sheppard","Sheppard","akani",1402.5f,574.5f,1396.6f,559.5f),T("RebeccaIsland","Rebecca Island","akani",1128.5f,564,1128,564.9f),T("Mariliand","Mariliand","birfani",747,570,741.4f,575.6f),T("BadenBaden","Baden Baden","birfani",924.5f,579,913.7f,577.6f),
      T("ValBay","Val Bay","flathia",270,617,267.3f,612),T("Potros","Potros","makatanga_islands",1035.5f,675,1031.2f,670.9f),T("Braveland","Braveland","flathia",337.5f,672.5f,337.6f,674.4f),T("TALand","T A Land","birfani",753.5f,693,767.9f,682.4f),
      T("Maki","Maki","makatanga_islands",1272.5f,713.5f,1261.1f,703.8f),T("MillaSunderland","Milla Sunderland","birfani",873,722,875.6f,717.2f),T("Putfire","Putfire","flathia",446.5f,725,440.5f,730.1f),T("CentralBase","Central Base","makatanga_islands",1095.5f,737,1096.5f,736.6f),
      T("Montreux","Montreux","flathia",299,738,305.5f,737.9f),T("DarknessRoom","Darkness Room","south_pole",655,807,650.3f,807.7f),T("Haeox","Haeox","makatanga_islands",997,806.5f,998,811.7f),T("Lineker","Lineker","makatanga_islands",1165.5f,820,1163.3f,817.6f),
      T("ColdCoast","Cold Coast","south_pole",803.5f,953,810.7f,956.4f),T("Trinit","Trinit","south_pole",940,956.5f,941.7f,959.3f),T("Garin","Garin","south_pole",418.5f,958,423.4f,962.5f),T("Tarantula","Tarantula","south_pole",678.5f,966,677,972),
      T("Demetrius","Demetrius","south_pole",541.5f,966.5f,546.6f,974.1f),T("OffRoad","Off Road","south_pole",1074.5f,969,1070.9f,974.7f),T("KsIsland","Ks Island","south_pole",1182,979,1189.3f,982.8f)};
    static readonly Dictionary<string,(string n,int b,int o,Color c)> Regions=new(StringComparer.Ordinal){["flathia"]=("Flathia",5,0,new Color(.4f,.8f,.3f)),["agla"]=("Agla",3,1,new Color(.3f,.7f,1f)),["vorlia"]=("Vorlia",2,2,new Color(.8f,.35f,.35f)),["akani"]=("Akani",3,3,new Color(.75f,.55f,.25f)),["birfani"]=("Birfani",3,4,new Color(.65f,.4f,.85f)),["makatanga_islands"]=("Makatanga Islands",2,5,new Color(.25f,.8f,.7f)),["south_pole"]=("South Pole",4,6,Color.white)};
    static readonly string[] Land="1-2 1-6 1-4 2-6 3-5 3-10 4-6 5-11 5-10 7-12 7-13 8-15 8-14 10-11 10-17 10-18 11-18 12-16 12-13 14-15 14-19 15-19 15-21 17-18 17-25 18-25 19-21 20-23 20-28 23-28 23-24 24-30 24-28 25-27 25-33 27-33 27-31 28-30 31-33 37-38 37-40 38-42 39-41 40-41 42-43".Split(' ');
    static readonly string[] Special="9-3 22-19 26-30 29-21 32-26 35-32 36-29 34-40 5-7 16-14 18-20 24-22 34-28".Split(' ');

    [MenuItem("War Dominion/Ferramentas de Desenvolvimento/Mapas/Gerar ou Atualizar/Dark World")]
    public static void Generate(){ValidateSources();var geometries=GenerateMasks();AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);ConfigureImports();var map=GenerateDefinition(geometries);UpdateCatalog(map);GenerateScene(map,geometries);AssetDatabase.SaveAssets();AssetDatabase.Refresh();Validate();Debug.Log($"[Mapa] Dark World Remaster gerado: 43 territórios, 7 regiões, {map.Conexoes.Count} conexões.");}
    [MenuItem("War Dominion/Ferramentas de Desenvolvimento/Mapas/Validar/Dark World")]
    public static void Validate(){var map=AssetDatabase.LoadAssetAtPath<DefinicaoMapa>(DefinitionPath);if(map==null)throw new InvalidOperationException("DarkWorld.asset ausente.");var issues=ValidadorDefinicaoMapa.Validar(map);if(issues.Count>0)throw new InvalidOperationException(string.Join(" | ",issues));if(map.Territorios.Count!=43||map.Regioes.Count!=7)throw new InvalidOperationException("Dark World deve possuir 43 territórios e 7 regiões.");ValidateScene();Debug.Log($"[Mapa] Dark World validado: 43/7/{map.Conexoes.Count}.");}
    static List<(string a,string b)> Connections(){return Land.Concat(Special).Select(s=>{var p=s.Split('-').Select(int.Parse).ToArray();return Pair(Territories[p[0]-1].Id,Territories[p[1]-1].Id);}).Distinct().ToList();}
    static (string a,string b) Pair(string a,string b)=>string.CompareOrdinal(a,b)<=0?(a,b):(b,a);
    static DefinicaoMapa GenerateDefinition(IReadOnlyDictionary<string,Geometry> geometries){var m=AssetDatabase.LoadAssetAtPath<DefinicaoMapa>(DefinitionPath);if(m==null)throw new InvalidOperationException("DarkWorld.asset ausente; a migração exige preservar seus dados lógicos.");var ts=m.Territorios.Select(t=>new DefinicaoTerritorioMapa{id=t.id,nomeExibido=t.nomeExibido,chaveTraducao=t.chaveTraducao,regiaoId=t.regiaoId,posicaoContador=geometries[t.id].Counter,possuiPosicaoContador=true,posicaoNomeManual=t.posicaoNomeManual,possuiPosicaoNomeManual=t.possuiPosicaoNomeManual,tamanhoFonteNome=t.tamanhoFonteNome,metadados=t.metadados}).ToList();var rs=m.Regioes.ToList();var cs=m.Conexoes.ToList();m.ConfigurarNoEditor(m.MapaId,m.NomeInterno,m.NomeExibido,m.Descricao,m.TemaId,AssetDatabase.LoadAssetAtPath<Sprite>(Art),m.CentroCamera,m.TamanhoOrtografico,ts,rs,cs);EditorUtility.SetDirty(m);AssetDatabase.SaveAssetIfDirty(m);return m;}
    static void UpdateCatalog(DefinicaoMapa map){var c=AssetDatabase.LoadAssetAtPath<CatalogoMapas>(CatalogPath);if(c==null||c.MapaPadrao==null)throw new InvalidOperationException("Catálogo ausente.");var maps=c.Mapas.Where(x=>x!=null&&x.MapaId!="dark_world").Distinct().ToList();maps.Add(map);c.ConfigurarNoEditor(c.MapaPadrao,maps);EditorUtility.SetDirty(c);AssetDatabase.SaveAssetIfDirty(c);}
    static void ConfigureImports(){Configure(Art,2048);Configure(Guide,2048);foreach(var t in Territories)Configure(Masks+"/"+t.Id+".png",1024);}
    static void Configure(string path,int max){var i=AssetImporter.GetAtPath(path) as TextureImporter;if(i==null)throw new InvalidOperationException("Importer ausente: "+path);i.textureType=TextureImporterType.Sprite;i.spriteImportMode=SpriteImportMode.Single;i.spritePixelsPerUnit=Ppu;i.alphaSource=TextureImporterAlphaSource.FromInput;i.alphaIsTransparency=true;i.mipmapEnabled=false;i.npotScale=TextureImporterNPOTScale.None;i.textureCompression=TextureImporterCompression.Uncompressed;i.maxTextureSize=max;i.filterMode=FilterMode.Bilinear;i.SaveAndReimport();}
    // ============================================================
    // 3. ATUALIZAÇÃO CONTROLADA DA CENA EXISTENTE
    // ============================================================
    static void GenerateScene(DefinicaoMapa map,IReadOnlyDictionary<string,Geometry> geometries)
    {
        var active=SceneManager.GetActiveScene();bool alreadyOpen=active.path==ScenePath;var scene=alreadyOpen?active:EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Additive);
        try
        {
            var clicks=scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<TerritorioClique>(true)).ToDictionary(x=>x.idTerritorio,StringComparer.Ordinal);
            if(clicks.Count!=43)throw new InvalidOperationException($"Cena Dark World existente contém {clicks.Count} territórios; esperado: 43.");
            foreach(var territory in Territories)Setup(clicks[territory.Id].gameObject,geometries[territory.Id]);
            var renderers=scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<SpriteRenderer>(true));
            var visual=renderers.FirstOrDefault(x=>x.gameObject.name=="DarkWorld_Arte")??renderers.FirstOrDefault(x=>x.sprite!=null&&x.sortingOrder>=2&&x.transform.parent==null);
            if(visual==null)throw new InvalidOperationException("Renderer da arte-base Dark World não encontrado.");
            visual.gameObject.name="DarkWorld_Arte";visual.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);visual.transform.localScale=Vector3.one;visual.sprite=map.ArteBase;visual.color=Color.white;visual.sortingOrder=2;
            var camera=scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<Camera>(true)).FirstOrDefault(x=>x.CompareTag("MainCamera"));
            if(camera!=null){camera.transform.position=new Vector3(map.CentroCamera.x,map.CentroCamera.y,camera.transform.position.z);camera.orthographicSize=map.TamanhoOrtografico;}
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene,ScenePath);
        }
        finally{if(!alreadyOpen)EditorSceneManager.CloseScene(scene,true);}
    }
    static void Setup(GameObject o,Geometry g){var t=g.Territory;o.name=t.Id;o.transform.SetLocalPositionAndRotation(new Vector3(g.Position.x,g.Position.y,0),Quaternion.identity);o.transform.localScale=Vector3.one;var s=AssetDatabase.LoadAssetAtPath<Sprite>(Masks+"/"+t.Id+".png");var r=o.GetComponent<SpriteRenderer>();r.sprite=s;r.color=Color.white;r.sortingOrder=0;var click=o.GetComponent<TerritorioClique>();click.idTerritorio=t.Id;click.chaveTraducao="territory_"+t.Id.ToLowerInvariant();click.continente=(TerritorioClique.Continente)Mathf.Clamp(Regions[t.Region].o,0,5);click.dono=TerritorioClique.Dono.Neutro;(o.GetComponent<TerritorioTropas>()??o.AddComponent<TerritorioTropas>()).Quantidade=1;o.GetComponent<TerritorioFronteiras>().Configurar(Array.Empty<TerritorioClique>());var old=o.GetComponent<PolygonCollider2D>();if(old!=null)UnityEngine.Object.DestroyImmediate(old);var col=o.AddComponent<PolygonCollider2D>();col.pathCount=s.GetPhysicsShapeCount();var pts=new List<Vector2>();for(int n=0;n<col.pathCount;n++){pts.Clear();s.GetPhysicsShape(n,pts);col.SetPath(n,pts);}var counter=o.transform.Find("ContadorModerno");counter.localPosition=new Vector3(g.Counter.x,g.Counter.y,0);}

    // ============================================================
    // 1. SEGMENTAÇÃO AUTORITATIVA DO GABARITO REMASTER
    // ============================================================
    static Dictionary<string,Geometry> GenerateMasks()
    {
        Directory.CreateDirectory(Path.GetFullPath(Masks));
        var texture=new Texture2D(2,2,TextureFormat.RGBA32,false);
        try
        {
            if(!ImageConversion.LoadImage(texture,File.ReadAllBytes(Path.GetFullPath(Guide))))throw new InvalidOperationException("Falha ao ler o gabarito remaster.");
            var source=texture.GetPixels32();var width=texture.width;var height=texture.height;
            var labels=Enumerable.Repeat(-1,width*height).ToArray();var queue=new int[width*height];var components=new List<List<int>>();
            bool IsInterior(int topIndex){int x=topIndex%width,y=topIndex/width;var c=source[(height-1-y)*width+x];return c.r>=252&&c.g>=252&&c.b>=252&&c.a>0;}
            for(int start=0;start<labels.Length;start++)
            {
                if(labels[start]>=0||!IsInterior(start))continue;
                int label=components.Count,head=0,tail=0;var pixels=new List<int>();labels[start]=label;queue[tail++]=start;
                while(head<tail)
                {
                    int current=queue[head++],x=current%width,y=current/width;pixels.Add(current);
                    void Add(int next){if(labels[next]<0&&IsInterior(next)){labels[next]=label;queue[tail++]=next;}}
                    if(x>0)Add(current-1);if(x+1<width)Add(current+1);if(y>0)Add(current-width);if(y+1<height)Add(current+width);
                }
                components.Add(pixels);
            }
            var relevant=components.Where(c=>c.Count>=500).ToList();
            if(relevant.Count!=43)throw new InvalidOperationException($"Gabarito remaster produziu {relevant.Count} componentes relevantes; esperado: 43 (RGB >= 252). ");
            var result=new Dictionary<string,Geometry>(StringComparer.Ordinal);
            var available=new HashSet<int>(Enumerable.Range(0,relevant.Count));
            foreach(var territory in Territories)
            {
                int selected=available.OrderBy(i=>DistanceSquared(Centroid(relevant[i],width),territory.AnchorPixel)).First();available.Remove(selected);
                var pixels=relevant[selected];var bounds=Bounds(pixels,width);var center=new Vector2((bounds.xMin+bounds.xMax)/2f,(bounds.yMin+bounds.yMax)/2f);
                int counterPixel=pixels.OrderBy(p=>DistanceSquared(new Vector2(p%width,p/width),territory.CounterPixel)).First();
                var counterTop=new Vector2(counterPixel%width,counterPixel/width);
                var geometry=new Geometry{Territory=territory,Bounds=bounds,Area=pixels.Count,Position=PixelToWorld(center),Counter=(counterTop-center)*new Vector2(1f,-1f)/Ppu};
                WriteMask(territory.Id,pixels,bounds,width,height);result.Add(territory.Id,geometry);
            }
            if(available.Count!=0||result.Count!=43)throw new InvalidOperationException("Associação geométrica Dark World incompleta.");
            return result;
        }
        finally{UnityEngine.Object.DestroyImmediate(texture);}
    }

    // ============================================================
    // 2. RECORTE, OFFSET E POSIÇÃO DO CONTADOR
    // ============================================================
    static Vector2 Centroid(List<int> pixels,int width){double x=0,y=0;foreach(int p in pixels){x+=p%width;y+=p/width;}return new Vector2((float)(x/pixels.Count),(float)(y/pixels.Count));}
    static float DistanceSquared(Vector2 a,Vector2 b)=>(a-b).sqrMagnitude;
    static RectInt Bounds(List<int> pixels,int width){int minX=int.MaxValue,minY=int.MaxValue,maxX=0,maxY=0;foreach(int p in pixels){int x=p%width,y=p/width;minX=Math.Min(minX,x);minY=Math.Min(minY,y);maxX=Math.Max(maxX,x);maxY=Math.Max(maxY,y);}return new RectInt(minX,minY,maxX-minX+1,maxY-minY+1);}
    static Vector2 PixelToWorld(Vector2 p)=>new Vector2((p.x-Width/2f)/Ppu,(Height/2f-p.y)/Ppu);
    static void WriteMask(string id,List<int> pixels,RectInt bounds,int sourceWidth,int sourceHeight)
    {
        var crop=new Texture2D(bounds.width,bounds.height,TextureFormat.RGBA32,false);var colors=new Color32[bounds.width*bounds.height];
        foreach(int p in pixels){int x=p%sourceWidth-bounds.xMin;int topY=p/sourceWidth-bounds.yMin;colors[(bounds.height-1-topY)*bounds.width+x]=new Color32(255,255,255,255);}
        crop.SetPixels32(colors);crop.Apply(false,false);
        try{File.WriteAllBytes(Path.GetFullPath(Masks+"/"+id+".png"),crop.EncodeToPNG());}
        finally{UnityEngine.Object.DestroyImmediate(crop);}
    }

    static void ValidateSources(){if(Dim(Art)!=new Vector2Int(1529,1019)||Dim(Guide)!=new Vector2Int(1529,1019))throw new InvalidOperationException("Arte/gabarito Dark World Remaster não estão 1:1 em 1529x1019.");if(Territories.Length!=43||Regions.Count!=7)throw new InvalidOperationException("Configuração 43/7 inválida.");}
    static Vector2Int Dim(string p){var t=new Texture2D(2,2);try{ImageConversion.LoadImage(t,File.ReadAllBytes(Path.GetFullPath(p)));return new Vector2Int(t.width,t.height);}finally{UnityEngine.Object.DestroyImmediate(t);}}
    static void ValidateScene(){var previous=SceneManager.GetActiveScene();bool open=previous.path==ScenePath;var scene=open?previous:EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Additive);try{var all=scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<TerritorioClique>(true)).ToArray();if(all.Length!=43||all.Select(x=>x.idTerritorio).Distinct().Count()!=43)throw new InvalidOperationException("Cena sem 43 IDs únicos.");foreach(var t in all){var c=t.GetComponent<PolygonCollider2D>();var n=t.transform.Find("ContadorModerno");if(c==null||c.pathCount==0||n==null||!c.OverlapPoint(n.position))throw new InvalidOperationException("Geometria/contador inválido: "+t.idTerritorio);}}finally{if(!open)EditorSceneManager.CloseScene(scene,true);}}
}
#endif

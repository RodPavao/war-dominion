#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PeloponesoEditor
{
    // ============================================================
    // 01. CAMINHOS E CONFIGURAÇÃO VISUAL
    // ============================================================
    const string Root="Assets/Mapas/Peloponeso", Masks=Root+"/Mascaras", Art=Root+"/Arte/Peloponeso.png", Guide=Root+"/Arte/Peloponeso_GabaritoTerritorios.png";
    const string ScenePath=Root+"/Peloponeso.unity", DefinitionPath="Assets/Resources/Mapas/Peloponeso.asset", CatalogPath="Assets/Resources/Mapas/CatalogoMapas.asset", BaseScene="Assets/WarGame.unity";
    const float Ppu=100f, CameraSize=5.25f;

    // ============================================================
    // 02. TERRITÓRIOS E REGIÕES OFICIAIS
    // ============================================================
    readonly struct Territory { public Territory(string id,string name,string region){Id=id;Name=name;Region=region;} public readonly string Id,Name,Region; }
    static Territory T(string id,string name,string region)=>new Territory(id,name,region);
    static IEnumerable<Territory> Group(string region,string values)=>values.Split(',').Select(v=>{var p=v.Split('|');return T(p[0],p.Length>1?p[1]:p[0].Replace("City"," City"),region);});
    static readonly Territory[] Territories=Group("laconia","Amyclae,Sparta,Sellasia,Gythium,Helos,Therapne,Zarax,Bryseae,EpidaurosLimera|Epidauros Limera,Boeae,Kardamyle,Oetylus,Tegea,Mantinea,Aegiae,Caryae,Asine,Las,Pitane,Thalamae,Alagonia")
      .Concat(Group("cyclades_islands","Kea,Ios,Gavrio,Chora,Serifos,Amorgos,Batsi,Kythnos,Folegandros,Sikinos,Anafi,Ormos,AndrosCity|Andros City,Naousa,Paros,Lefkes,Marpissa,Portara,NaxosCity|Naxos City,Melanes,Aperanthos,Sangri,Filoti,Tinos,Milos,Syros,Delos"))
      .Concat(Group("atica","Marathon,Piraeus,Eleusis,Acharnae,Brauron,Thorikos,Rhamnous,Sounion,Athens,Kolonos,Laurion,Phyle,Sphettos,Kifisia,Salamis,Anaphlystos,Icaria,Oropus,Deceleia,Aphidnae,Paiania,Myrrhinous,Lamptrai,Euonymon,HalaiAixonides|Halai Aixonides"))
      .Concat(Group("creta","Heraklion,Knossos,Chania,Rethymno,Kissamos,Ierapetra,Phaistos,Malia,Gortyna")).ToArray();
    static readonly Dictionary<string,(string name,int bonus,int order,Color color)> Regions=new(StringComparer.Ordinal){["laconia"]=("Laconia",10,0,new Color(.85f,.35f,.25f)),["cyclades_islands"]=("Cyclades Islands",12,1,new Color(.3f,.7f,1f)),["atica"]=("Atica",12,2,new Color(.35f,.8f,.35f)),["creta"]=("Creta",4,3,new Color(.8f,.7f,.25f))};

    // Ordem espacial dos componentes no gabarito, de cima para baixo e esquerda para direita.
    static readonly string[] SpatialIds="Marathon Amyclae Kea Sparta Ios Piraeus Gavrio Eleusis Acharnae Brauron Chora Serifos Sellasia Amorgos Gythium Batsi Thorikos Kythnos Rhamnous Folegandros Helos Therapne Sikinos Sounion Anafi Athens Zarax Ormos AndrosCity Bryseae EpidaurosLimera Kolonos Laurion Phyle Naousa Boeae Kardamyle Paros Lefkes Oetylus Marpissa Portara Tegea Sphettos Kifisia Salamis NaxosCity Melanes Aperanthos Sangri Filoti Mantinea Anaphlystos Tinos Aegiae Milos Icaria Oropus Syros Delos Caryae Deceleia Asine Aphidnae Paiania Myrrhinous Las Pitane Lamptrai Euonymon HalaiAixonides Thalamae Heraklion Alagonia Knossos Chania Rethymno Kissamos Ierapetra Phaistos Malia Gortyna".Split(' ');

    // ============================================================
    // 03. FRONTEIRAS E CONECTORES VISUAIS
    // ============================================================
    static readonly string[] Land="1-9 1-6 1-8 1-10 2-15 2-4 3-5 3-14 3-11 4-13 5-16 5-14 5-7 6-9 7-12 7-20 7-16 8-10 9-17 10-24 10-19 11-14 11-23 12-18 12-20 13-22 13-21 13-15 14-16 14-23 15-21 16-20 18-20 18-25 19-24 20-25 20-28 21-22 21-30 22-30 22-27 23-29 24-34 24-26 24-33 26-32 26-33 27-31 27-36 27-30 28-29 30-36 30-37 31-40 31-36 32-33 32-44 33-44 33-34 33-46 34-46 34-45 36-43 36-37 36-40 37-43 38-39 38-49 39-49 39-51 40-52 40-43 41-47 41-42 41-50 42-47 42-48 43-55 43-52 44-46 44-57 44-58 45-46 45-53 46-58 46-53 47-50 47-54 48-56 48-54 49-51 50-59 52-61 52-55 53-62 53-64 54-56 54-60 54-59 55-61 55-63 56-60 57-58 57-65 58-66 58-65 58-62 59-60 61-63 61-68 62-66 62-64 62-71 63-67 63-72 63-68 64-69 64-71 65-70 65-66 66-70 66-71 67-72 68-72 68-74 69-71 70-71 72-74 73-75 73-78 75-79 75-78 76-80 76-77 77-81 77-80 77-78 78-79 78-82 78-81 80-81 81-82".Split(' ');
    static readonly string[] Special="15-7 30-38 43-49 72-73 23-17 29-45 35-34 60-53 79-69".Split(' ');
    static List<(string a,string b)> Connections()=>Land.Concat(Special).Select(s=>{var p=s.Split('-').Select(int.Parse).ToArray();string a=SpatialIds[p[0]-1],b=SpatialIds[p[1]-1];return string.CompareOrdinal(a,b)<=0?(a,b):(b,a);}).Distinct().ToList();

    // ============================================================
    // 04. MENUS DE GERAÇÃO E VALIDAÇÃO
    // ============================================================
    [MenuItem("War Dominion/Ferramentas de Desenvolvimento/Mapas/Gerar ou Atualizar/Peloponeso")]
    public static void Generate(){ValidateSources();AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);ConfigureImports();var map=GenerateDefinition();UpdateCatalog(map);GenerateScene(map);AssetDatabase.SaveAssets();AssetDatabase.Refresh();Validate();Debug.Log($"[Mapa] Peloponeso gerado: 82 territórios, 4 regiões e {Connections().Count} conexões.");}
    [MenuItem("War Dominion/Ferramentas de Desenvolvimento/Mapas/Validar/Peloponeso")]
    public static void Validate(){var map=AssetDatabase.LoadAssetAtPath<DefinicaoMapa>(DefinitionPath);if(map==null)throw new InvalidOperationException("Peloponeso.asset ausente.");var issues=ValidadorDefinicaoMapa.Validar(map);if(issues.Count>0)throw new InvalidOperationException(string.Join(" | ",issues));if(map.Territorios.Count!=82||map.Regioes.Count!=4)throw new InvalidOperationException("Peloponeso deve possuir 82 territórios e 4 regiões.");ValidateScene();Debug.Log($"[Mapa] Peloponeso validado: 82/4/{map.Conexoes.Count}.");}

    // ============================================================
    // 05. DEFINIÇÃO E CATÁLOGO
    // ============================================================
    static DefinicaoMapa GenerateDefinition(){var ts=Territories.Select(t=>new DefinicaoTerritorioMapa{id=t.Id,nomeExibido=t.Name,chaveTraducao="territory_"+t.Id.ToLowerInvariant(),regiaoId=t.Region,posicaoContador=MaskCenter(t.Id),possuiPosicaoContador=true}).ToList();var rs=Regions.Select(r=>new DefinicaoRegiaoMapa{id=r.Key,nomeExibido=r.Value.name,descricao=r.Value.name,chaveTraducao="region_"+r.Key,bonus=r.Value.bonus,ordemExibicao=r.Value.order,corDestaque=r.Value.color,territorioIds=Territories.Where(t=>t.Region==r.Key).Select(t=>t.Id).ToList()}).OrderBy(r=>r.ordemExibicao).ToList();var cs=Connections().Select(p=>new DefinicaoConexaoMapa{origemId=p.a,destinoId=p.b,tipo=TipoConexaoMapa.Terrestre,bidirecional=true}).ToList();var m=AssetDatabase.LoadAssetAtPath<DefinicaoMapa>(DefinitionPath);if(m==null){m=ScriptableObject.CreateInstance<DefinicaoMapa>();AssetDatabase.CreateAsset(m,DefinitionPath);}m.ConfigurarNoEditor("peloponeso","Peloponeso","Peloponeso","Mapa completo da Guerra do Peloponeso com 82 territórios.","peloponeso",AssetDatabase.LoadAssetAtPath<Sprite>(Art),Vector2.zero,CameraSize,ts,rs,cs);EditorUtility.SetDirty(m);AssetDatabase.SaveAssetIfDirty(m);return m;}
    static void UpdateCatalog(DefinicaoMapa map){var c=AssetDatabase.LoadAssetAtPath<CatalogoMapas>(CatalogPath);if(c==null||c.MapaPadrao==null)throw new InvalidOperationException("Catálogo ausente.");var maps=c.Mapas.Where(x=>x!=null&&x.MapaId!="peloponeso").Distinct().ToList();maps.Add(map);c.ConfigurarNoEditor(c.MapaPadrao,maps);EditorUtility.SetDirty(c);AssetDatabase.SaveAssetIfDirty(c);}

    // ============================================================
    // 06. IMPORTAÇÃO E CENA COMPLETA
    // ============================================================
    static void ConfigureImports(){Configure(Art,2048);foreach(var t in Territories)Configure(Masks+"/"+t.Id+".png",2048);}
    static void Configure(string path,int max){var i=AssetImporter.GetAtPath(path) as TextureImporter;if(i==null)throw new InvalidOperationException("Importer ausente: "+path);i.textureType=TextureImporterType.Sprite;i.spriteImportMode=SpriteImportMode.Single;i.spritePixelsPerUnit=Ppu;i.alphaSource=TextureImporterAlphaSource.FromInput;i.alphaIsTransparency=true;i.mipmapEnabled=false;i.npotScale=TextureImporterNPOTScale.None;i.textureCompression=TextureImporterCompression.Uncompressed;i.maxTextureSize=max;i.filterMode=FilterMode.Bilinear;i.SaveAndReimport();}
    static void GenerateScene(DefinicaoMapa map){if(AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath)!=null&&!AssetDatabase.DeleteAsset(ScenePath))throw new InvalidOperationException("Falha ao substituir cena.");if(!AssetDatabase.CopyAsset(BaseScene,ScenePath))throw new InvalidOperationException("Falha ao copiar cena base.");var scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);var parent=GameObject.Find("Territorios");var model=parent?.transform.Find("Arabia");if(model==null)throw new InvalidOperationException("Molde ausente.");parent.transform.SetLocalPositionAndRotation(Vector3.zero,Quaternion.identity);parent.transform.localScale=Vector3.one;var news=new List<GameObject>();for(int n=0;n<82;n++){var o=UnityEngine.Object.Instantiate(model.gameObject,parent.transform,false);o.name="__Peloponeso_"+n;news.Add(o);}for(int n=parent.transform.childCount-1;n>=0;n--)if(!parent.transform.GetChild(n).name.StartsWith("__Peloponeso_",StringComparison.Ordinal))UnityEngine.Object.DestroyImmediate(parent.transform.GetChild(n).gameObject);for(int n=0;n<82;n++)Setup(news[n],Territories[n]);var visual=GameObject.Find("classic_0");visual.name="Peloponeso_Arte";visual.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);visual.transform.localScale=Vector3.one;var vr=visual.GetComponent<SpriteRenderer>();vr.sprite=map.ArteBase;vr.color=Color.white;vr.sortingOrder=2;if(Camera.main!=null){Camera.main.transform.position=new Vector3(0,0,Camera.main.transform.position.z);Camera.main.orthographicSize=CameraSize;}EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene,ScenePath);}
    static void Setup(GameObject o,Territory t){o.name=t.Id;o.transform.SetLocalPositionAndRotation(Vector3.zero,Quaternion.identity);o.transform.localScale=Vector3.one;var s=AssetDatabase.LoadAssetAtPath<Sprite>(Masks+"/"+t.Id+".png");var r=o.GetComponent<SpriteRenderer>();r.sprite=s;r.color=Color.white;r.sortingOrder=0;var click=o.GetComponent<TerritorioClique>();click.idTerritorio=t.Id;click.chaveTraducao="territory_"+t.Id.ToLowerInvariant();click.continente=(TerritorioClique.Continente)Mathf.Clamp(Regions[t.Region].order,0,5);click.dono=TerritorioClique.Dono.Neutro;(o.GetComponent<TerritorioTropas>()??o.AddComponent<TerritorioTropas>()).Quantidade=1;o.GetComponent<TerritorioFronteiras>().Configurar(Array.Empty<TerritorioClique>());var old=o.GetComponent<PolygonCollider2D>();if(old!=null)UnityEngine.Object.DestroyImmediate(old);var col=o.AddComponent<PolygonCollider2D>();col.pathCount=s.GetPhysicsShapeCount();var pts=new List<Vector2>();for(int n=0;n<col.pathCount;n++){pts.Clear();s.GetPhysicsShape(n,pts);col.SetPath(n,pts);}var counter=o.transform.Find("ContadorModerno");counter.localPosition=SafeCenter(col);counter.localScale=new Vector3(0.78f,0.72f,2f);}
    static Vector3 SafeCenter(PolygonCollider2D col){int best=0;float area=-1;for(int i=0;i<col.pathCount;i++){var p=col.GetPath(i);var b=Bounds(p);float a=b.size.x*b.size.y;if(a>area){area=a;best=i;}}var bounds=Bounds(col.GetPath(best));return new Vector3(bounds.center.x,bounds.center.y,0);}
    static Bounds Bounds(Vector2[] points){var b=new Bounds(points[0],Vector3.zero);foreach(var p in points)b.Encapsulate(p);return b;}
    static Vector2 MaskCenter(string id){var s=AssetDatabase.LoadAssetAtPath<Sprite>(Masks+"/"+id+".png");if(s==null||s.GetPhysicsShapeCount()==0)throw new InvalidOperationException("Physics shape ausente: "+id);var pts=new List<Vector2>();s.GetPhysicsShape(0,pts);return Bounds(pts.ToArray()).center;}

    // ============================================================
    // 07. VALIDAÇÕES E ISOLAMENTO
    // ============================================================
    static void ValidateSources(){if(Dim(Art)!=new Vector2Int(1536,1024)||Dim(Guide)!=new Vector2Int(1536,1024))throw new InvalidOperationException("Arte/gabarito não estão 1:1 em 1536x1024.");if(Territories.Length!=82||Territories.Select(t=>t.Id).Distinct().Count()!=82)throw new InvalidOperationException("IDs 82 inválidos.");foreach(var t in Territories)if(!File.Exists(Path.GetFullPath(Masks+"/"+t.Id+".png")))throw new FileNotFoundException(t.Id);}
    static Vector2Int Dim(string p){var t=new Texture2D(2,2);try{ImageConversion.LoadImage(t,File.ReadAllBytes(Path.GetFullPath(p)));return new Vector2Int(t.width,t.height);}finally{UnityEngine.Object.DestroyImmediate(t);}}
    static void ValidateScene(){var previous=SceneManager.GetActiveScene();bool open=previous.path==ScenePath;var scene=open?previous:EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Additive);try{var all=scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<TerritorioClique>(true)).ToArray();if(all.Length!=82||all.Select(x=>x.idTerritorio).Distinct().Count()!=82)throw new InvalidOperationException("Cena sem 82 IDs únicos.");foreach(var t in all){var c=t.GetComponent<PolygonCollider2D>();var n=t.transform.Find("ContadorModerno");if(c==null||c.pathCount==0||n==null)throw new InvalidOperationException("Geometria/contador inválido: "+t.idTerritorio);}}finally{if(!open)EditorSceneManager.CloseScene(scene,true);}}
}
#endif

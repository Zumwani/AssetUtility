#if UNITY_EDITOR

public class DocumentationRef_GeneratedClass : AssetUtility.Documentation.DocumentationViewer
{

    //An id which is used to locate documentation folder, using ScriptableObject with same id string variable
    protected override string id => "bf777176c37a70a49a7ab6daa162aa9f";
    protected override string homeFile => "home.md";
    protected override string sidebarFile => "_sidebar.md";

    [UnityEditor.MenuItem("Tools/test/Documentation")]
    static void Open() =>
        Open<DocumentationRef_GeneratedClass>(title: "test - Documentation");

}

#endif

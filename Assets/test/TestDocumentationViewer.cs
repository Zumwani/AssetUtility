using UnityEditor;

public class TestDocumentationViewer : DocumentationViewer
{

    protected override string documentationPath => "wiki";
    protected override string homeFile => "home.md";
    protected override string sidebarFile => "_sidebar.md";

    [MenuItem("Tools/Test/Documentation")]
    public static void Open() =>
        Open<TestDocumentationViewer>(title: "Test - Documentation");

}

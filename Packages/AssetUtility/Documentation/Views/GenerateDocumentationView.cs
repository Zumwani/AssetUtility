using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace AssetUtility.Documentation
{

    class GenerateDocumentationView : View
    {

        public override string header { get; } = "Generate documentation";

        public string path = "Assets/Documentation";
        public string[] addedProjects;
        [NonSerialized] public Project[] projects;

        [Serializable]
        public class Project
        {
            public string path;
            public string documentationFile;
            public bool isAdded; //Include in auto generation
        }

        public override void OnEnable()
        {
            context = SynchronizationContext.Current;
            Load();
        }

        public override void OnDisable() =>
            Save();

        public override void OnGUI()
        {

            if (projects is null || projects.Length == 0)
                RefreshProjects();

            EditorGUILayout.HelpBox("Can only be used with Visual Studio", MessageType.Warning);

            GUILayout.BeginVertical(new GUIStyle(EditorStyles.helpBox) { padding = new RectOffset(12, 12, 12, 12) });
            EditorGUILayout.LabelField("Projects to generate documentation for:", EditorStyles.boldLabel);
            EditorGUILayout.Space();
            if (projects != null)
                foreach (var project in projects)
                    DrawProject(project);
            GUILayout.EndVertical();

            EditorGUILayout.Space();
            path = EditorGUILayout.TextField("Output Folder:", path);
            EditorGUILayout.Space();

            if (GUILayout.Button("Generate"))
                Generate();

        }

        SynchronizationContext context;
        async void Generate()
        {

            var currentFile = "";
            var i = 0;
            var max = 0;
            var tasks = projects.Where(p => p.isAdded).Select(p => GenerateSandCastleFile(p.path)).ToArray();
            max = tasks.Count();

            await Task.WhenAll(tasks);

            EditorUtility.ClearProgressBar();

            Task GenerateSandCastleFile(string project) =>
                Task.Run(() =>
                {

                    Progress(file: project);

                    var msBuild1 = Directory.GetFiles(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Microsoft Visual Studio"), "msbuild.exe", SearchOption.AllDirectories).FirstOrDefault();
                    var msBuild2 = Directory.GetFiles(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Microsoft Visual Studio"), "msbuild.exe", SearchOption.AllDirectories).FirstOrDefault();
                    var msBuild = msBuild1 ?? msBuild2;

                    var p = Process.Start(new ProcessStartInfo(msBuild, @$"""{project}""") { CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden });
                    p.WaitForExit();

                    Progress(step: true);

                });

            void Progress(bool step = false, string file = null) =>
                context.Post(new SendOrPostCallback(_ =>
                {

                    if (file != null) currentFile = file;
                    if (step) i += 1;
                    EditorUtility.DisplayProgressBar("Generating documentation...", currentFile, (float)i / max);
                    EditorApplication.Step();
                    EditorApplication.QueuePlayerLoopUpdate();

                }), null);

        }

        #region Projects

        void DrawProject(Project project)
        {
            var isAdded = EditorGUILayout.Toggle(Path.GetFileNameWithoutExtension(project.path), project.isAdded);
            if (isAdded != project.isAdded)
            {

                if (isAdded)
                {
                    ArrayUtility.Add(ref addedProjects, project.path);
                    EnableDocumentationFile(project);
                }
                else
                    ArrayUtility.Remove(ref addedProjects, project.path);

                project.isAdded = isAdded;

            }
        }

        void RefreshProjects()
        {
            var folder = Directory.GetParent(Application.dataPath);
            projects =
                Directory.GetFiles(folder.FullName, "*.csproj").
                Select(path => new Project()
                {
                    path = path,
                    isAdded = addedProjects.Contains(path),
                    documentationFile = GetDocumentationFile(path)
                }).
            ToArray();
        }

        const string startTag = "<DocumentationFile>";
        const string endTag = "</DocumentationFile>";

        string GetDocumentationFile(string projectPath)
        {

            if (!projectPath.EndsWith(".csproj"))
                return null;

            var file = File.ReadAllText(projectPath);
            if (!file.Contains(startTag))
                return null;

            var s = file.Substring(file.IndexOf(startTag) + startTag.Length);
            s = s.Remove(s.IndexOf(endTag));
            return s;

        }

        void EnableDocumentationFile(Project project)
        {

            if (!string.IsNullOrEmpty(project.documentationFile))
                return;

            var file = File.ReadAllText(project.path);

            //Set to default value when checking toggle in project settings
            project.documentationFile = @$"Temp\Bin\Debug\{Path.GetFileNameWithoutExtension(project.path)}.xml";

            var startTag = "<PropertyGroup>";
            var endTag = "</PropertyGroup>";

            var startIndex = file.IndexOf(startTag);
            file = file.Insert(startIndex,
                startTag + "\n\t\t" +
                GenerateDocumentationView.startTag + project.documentationFile + GenerateDocumentationView.endTag + "\n" +
                "\t" + endTag + "\n\t");
            File.WriteAllText(project.path, file);

        }

        #endregion

    }

}

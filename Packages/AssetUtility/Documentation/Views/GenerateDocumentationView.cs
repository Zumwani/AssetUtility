﻿using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Xml.XPath;
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

            EditorGUI.BeginChangeCheck();

            GUILayout.BeginVertical(new GUIStyle(EditorStyles.helpBox) { padding = new RectOffset(12, 12, 12, 12) });
            EditorGUILayout.LabelField("Projects to generate documentation for:", EditorStyles.boldLabel);
            EditorGUILayout.Space();
            if (projects != null)
                foreach (var project in projects)
                    DrawProject(project);
            GUILayout.EndVertical();

            EditorGUILayout.Space();
            path = EditorGUILayout.TextField("Output Folder:", path);

            if (EditorGUI.EndChangeCheck())
                Save();

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
            var tasks = projects.Where(p => p.isAdded).
                Select(async p =>
                {

                    await GenerateSandCastleFile(p);

                    var docs = ConvertSandcastleToMarkdown(p);
                    if (docs != null)
                        foreach (var type in docs)
                            WriteFile(type.markdown, path + "/" + Path.GetFileNameWithoutExtension(p.path) + "/" + type.type.Name + ".md");

                });

            max = tasks.Count();

            await Task.WhenAll(tasks);
            EditorUtility.ClearProgressBar();
            AssetDatabase.Refresh();

            Task GenerateSandCastleFile(Project project) =>
                Task.Run(() =>
                {

                    Progress(file: project.path);

                    EnableDocumentationFile(project);
                    var msBuild1 = Directory.GetFiles(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Microsoft Visual Studio"), "msbuild.exe", SearchOption.AllDirectories).FirstOrDefault();
                    var msBuild2 = Directory.GetFiles(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Microsoft Visual Studio"), "msbuild.exe", SearchOption.AllDirectories).FirstOrDefault();
                    var msBuild = msBuild1 ?? msBuild2;

                    var p = Process.Start(new ProcessStartInfo(msBuild, @$"""{project.path}""") { CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden });
                    p.WaitForExit();

                    Progress(step: true);

                });

            (Type type, string markdown)[] ConvertSandcastleToMarkdown(Project project)
            {

                try
                {

                    if (!File.Exists(project.documentationFile))
                        return default;

                    var sandcastle = XDocument.Load(project.documentationFile);
                    var members = sandcastle.Root.XPathSelectElements("/doc/members/member").ToArray();

                    var types = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == Path.GetFileNameWithoutExtension(project.path)).ExportedTypes;
                    var typesDoc = types.
                    Select(t => (type: t, description: Parse(FindDocumentation(t)))).
                    Where(kvp => !string.IsNullOrWhiteSpace(kvp.description)).
                    ToArray();

                    return typesDoc;

                    XElement FindDocumentation(Type type, MemberInfo member = null)
                    {

                        var name = (ToID(member) ?? "T") + ":" + type.FullName + (member != null ? "." + member.Name : "");

                        var genericTypeCount = ((member as MethodInfo)?.GetGenericArguments() ?? type.GetGenericArguments())?.Count() ?? 0;
                        if (genericTypeCount != 0)
                            name += "`" + genericTypeCount;

                        return FindDocumentationByID(name);

                    }

                    XElement FindDocumentationByID(string id) =>
                        members.FirstOrDefault(e => e.Attribute("name").Value == id);

                    string IDToName(string id)
                    {

                        var name = id;
                        if (id.EndsWith(")"))
                            name = name.Remove(name.IndexOf('('));

                        name = name.Substring(name.LastIndexOf('.') + 1);

                        if (id.Contains("("))
                            return name + id.Substring(id.IndexOf('('));
                        else if (id.StartsWith("M:"))
                            return name + "()";
                        else
                            return name;

                    }

                    string Parse(XElement element)
                    {

                        if (element is null)
                            return null;

                        var str = element.ToString().
                        Replace("<summary>", "\n").Replace("</summary>", "").
                        Replace("<para>", "\n\n").Replace("</para>", "").
                        Replace("<list type=\"bullet\">", "").Replace("<list type=\"number\">", "").Replace("<list type=\"table\">", "").Replace("<list>", "").Replace("</list>", "\n").
                        Replace("<item>", "\n* ").Replace("</item>", "").
                        Replace("<term>", "").Replace("</term>", ": ").
                        Replace("<description>", "").Replace("</description>", "");

                        while (str.Contains("<code>"))
                        {
                            var snippet = str.Remove(str.IndexOf("<code>"));
                            //Add 'Example' header to code block
                            if (snippet.Last() == '\n')
                                str = str.Replace("<code>", "\n#### Example:\n```csharp\n").Replace("</code>", "\n```\n");
                            else
                                str = str.Replace("<code>", "\n```csharp\n").Replace("</code>", "\n```\n\n");
                        }

                        while (str.Contains("<remarks>"))
                        {

                            var snippet = str.Substring(str.IndexOf("<remarks>"));
                            snippet = snippet.Remove(snippet.IndexOf("</remarks>") + "</remarks>".Length);
                            var originalSnippet = snippet;

                            //snippet = snippet.Replace("\n\n", "\\");
                            snippet = snippet.Replace("<remarks>\n", "\n> ").Replace("<remarks>", "> ").Replace("</remarks>", "\n\n");

                            var lines = snippet.Split('\n').Select(l => l.Trim()).ToArray();
                            var i2 = 0;
                            for (int i = 0; i < lines.Length; i++)
                            {

                                if (i2 == 2)
                                    lines[i] = ">\n" + ">" + lines[i];

                                if (string.IsNullOrWhiteSpace(lines[i]))
                                    i2 += 1;
                                else
                                    i2 = 0;

                            }

                            snippet = ">" + string.Join("\n", lines.Where(l => l.Trim().Length > 1));
                            snippet = snippet.Replace("\n>", ">").Replace(">", "\n>").TrimEnd('>');


                            str = str.Replace(originalSnippet, snippet + "\n");

                        }

                        str = str.Replace(Environment.NewLine, "").Replace("  ", "");
                        str = str.Replace("</member>", "");

                        //Type name
                        var match = Regex.Match(str, pattern: "<member name=\"(.*)\">");
                        var typeNameID = match.Groups[1].Value;
                        var typeName = IDToName(typeNameID);

                        str = str.Replace($"<member name=\"{typeNameID}\">", "## " + typeName);

                        //<see cref=""/>
                        while (str.Contains("<see cref"))
                        {
                            match = Regex.Match(str, pattern: "<see cref=\"(.*)\"?/>");
                            str = str.Replace(match.Groups[0].Value, IDToName(match.Groups[1].Value.Trim().TrimEnd('"')));
                        }

                        str = str.Replace("\n", Environment.NewLine);

                        return str;

                    }

                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogError(ex);
                }

                return default;

            }

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

        static string ToID(MemberInfo member)
        {

            if (member is MethodInfo)
                return "M";
            else if (member is FieldInfo)
                return "F";
            else if (member is PropertyInfo)
                return "P";
            else if (member is EventInfo)
                return "E";
            else
                return null;

        }

        static void WriteFile(string markdown, string file)
        {
            Directory.GetParent(file).Create();
            File.WriteAllText(file, markdown);
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

        #region Sandcastle to markdown



        #endregion

    }

}

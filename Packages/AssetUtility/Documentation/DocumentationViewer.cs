﻿#pragma warning disable IDE0052 // Remove unread private members
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;

public abstract partial class DocumentationViewer : EditorWindow
{

    //TODO: Generate documentation from xml comments

    GUISkin lightSkin;
    GUISkin darkSkin;

    /// <summary>Opens the viewer, with the specified title.</summary>
    protected static void Open<T>(string title = "Documentation") where T : DocumentationViewer
    {
        var w = GetWindow<T>();
        w.titleContent = new GUIContent(title);
        if (w.position.x > Screen.width || w.position.y > Screen.height || w.position.x < 0 || w.position.y < 0)
            w.position = new Rect((Screen.width / 2) - (w.position.width / 2), (Screen.height / 2) - (w.position.height / 2), w.position.width, w.position.height);
    }

    public static bool GetWindowInstance(out DocumentationViewer instance) =>
        instance = Resources.FindObjectsOfTypeAll<DocumentationViewer>().FirstOrDefault();

    /// <summary>The path to the folder which contains the documentation.</summary>
    protected abstract string documentationPath { get; }

    /// <summary>The path to the home file.</summary>
    protected abstract string homeFile { get; }

    /// <summary>The path to the sidebar file. Set to null to hide sidebar.</summary>
    protected abstract string sidebarFile { get; }

    string sidebar;
    string file;

    void OnEnable()
    {
        lightSkin = AssetDatabase.LoadAssetAtPath<GUISkin>($"Packages/{UnityMarkdownViewer.packageName}/Editor/Skin/MarkdownViewerSkin.guiskin");
        darkSkin = AssetDatabase.LoadAssetAtPath<GUISkin>($"Packages/{UnityMarkdownViewer.packageName}/Editor/Skin/MarkdownSkinQs.guiskin");
        Events.registeredPackages += Events_registeredPackages;
        UnityMarkdownViewer.Refresh();
        RegisterhyperLinkEvent();
    }

    void OnDisable()
    {
        Events.registeredPackages -= Events_registeredPackages;
        RegisterhyperLinkEvent(register: false);
    }

    void Events_registeredPackages(PackageRegistrationEventArgs e) =>
        Repaint();

    void Update()
    {
        OnViewUpdate();
    }

    void OnGUI()
    {

        minSize = new Vector2(651, 436);
        maxSize = new Vector2(4000, 4000);

        EnsureCorrectPath(ref file, homeFile);
        EnsureCorrectPath(ref sidebar, sidebarFile);

        if (!UnityMarkdownViewer.isInstalled.HasValue)
            UnityMarkdownViewer.Refresh();

        if (UnityMarkdownViewer.isInstalled ?? false)
            OnView();
        else if (UnityMarkdownViewer.isRefreshing || UnityMarkdownViewer.isInstalling)
            OnRefreshing();
        else if (UnityMarkdownViewer.error != null)
            OnError();
        else
            OnInstall();

    }

    void EnsureCorrectPath(ref string path, string nullPath)
    {

        var docPath = documentationPath;

        if (!docPath.StartsWith("Assets/")) docPath = "Assets/" + documentationPath;
        if (path is null) path = nullPath;
        if (!path.StartsWith(docPath)) path = docPath + "/" + path.TrimStart('/');
        if (!path.EndsWith(".md")) path += ".md";

    }

    #region Installing markdown viewer

    void OnInstall()
    {

        maxSize = minSize;

        GUILayout.BeginArea(new Rect(0, 0, Screen.width, Screen.height));
        GUILayout.FlexibleSpace();

        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();

        GUILayout.Label("The package <b>Unity Markdown Viewer</b> is required to view documentation in editor.", new GUIStyle(EditorStyles.label) { richText = true });

        //'Unity Markdown Viewer' link
        var rect = GUILayoutUtility.GetLastRect();

        var l1 = EditorStyles.label.CalcSize(new GUIContent("The package "));
        var l2 = EditorStyles.boldLabel.CalcSize(new GUIContent("Unity Markdown Viewer"));

        var r = new Rect(rect.xMin + l1.x, rect.yMin, l2.x, rect.height);

        if (GUI.Button(r, new GUIContent("", UnityMarkdownViewer.repoUri), GUIStyle.none))
            Application.OpenURL(UnityMarkdownViewer.repoUri);
        EditorGUIUtility.AddCursorRect(r, MouseCursor.Link);

        if (GUILayout.Button("Install now", new GUIStyle(GUI.skin.button) { margin = new RectOffset(0, 0, 0, bottom: 2) }))
            UnityMarkdownViewer.Install();

        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();

        GUILayout.Label("You can also view files in a separate markdown viewer app, by opening files manually, if one is installed.");

        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();

        GUILayout.FlexibleSpace();
        GUILayout.EndArea();

    }

    const string progressSpinners = "◜◠◝◞◡◟";
    int progressSpinnerIndex;
    double? time;

    void ProgressSpinner()
    {

        if (!time.HasValue || EditorApplication.timeSinceStartup - time > 0.05f)
        {
            progressSpinnerIndex += 1;
            time = EditorApplication.timeSinceStartup;
        }

        if (progressSpinnerIndex >= progressSpinners.Length)
            progressSpinnerIndex = 0;
        GUILayout.Label(progressSpinners[progressSpinnerIndex].ToString(), new GUIStyle(EditorStyles.label) { fontSize = 26 });
        Repaint();

    }

    void OnRefreshing()
    {

        GUILayout.BeginArea(new Rect(0, 0, Screen.width, Screen.height));
        GUILayout.FlexibleSpace();

        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();

        ProgressSpinner();

        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();

        GUILayout.FlexibleSpace();
        GUILayout.EndArea();

    }

    void OnError()
    {

        GUILayout.BeginArea(new Rect(0, 0, Screen.width, Screen.height));
        GUILayout.FlexibleSpace();

        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();

        GUILayout.Label($"An error occured (code: {UnityMarkdownViewer.error.errorCode})." + Environment.NewLine + UnityMarkdownViewer.error.message, new GUIStyle(EditorStyles.label) { richText = true });

        if (UnityMarkdownViewer.isRefreshError && GUILayout.Button("Retry"))
            UnityMarkdownViewer.Refresh();
        else if (UnityMarkdownViewer.isInstallError && GUILayout.Button("Retry"))
            UnityMarkdownViewer.Install();

        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();

        GUILayout.FlexibleSpace();
        GUILayout.EndArea();

    }

    #endregion
    #region Render markdown

#if UNITY_MARKDOWN_VIEWER

    MG.MDV.MarkdownViewer sidebarViewer;
    MG.MDV.MarkdownViewer mainViewer;

#endif

    void RegisterhyperLinkEvent(bool register = true)
    {

#if UNITY_MARKDOWN_VIEWER

        MG.MDV.HyperlinkHelper.HyperlinkOpened -= HyperlinkHelper_HyperlinkOpened;
        if (register)
            MG.MDV.HyperlinkHelper.HyperlinkOpened += HyperlinkHelper_HyperlinkOpened;

#endif

    }

#if UNITY_MARKDOWN_VIEWER
    void HyperlinkHelper_HyperlinkOpened(MG.MDV.HyperlinkOpenEventArgs e)
    {

        if (!e.IsMarkdownFile)
            return;

        e.Cancel = true;

        var file = e.Hyperlink;
        EnsureCorrectPath(ref file, homeFile);
        if (this.file != file)
        {
            this.file = file;
            mainViewer = null;
        }

    }
#endif

    void OnViewUpdate()
    {

#if UNITY_MARKDOWN_VIEWER
        if ((sidebarViewer?.Update() ?? false) || (mainViewer?.Update() ?? false))
            Repaint();
#endif

    }

    void OnView()
    {

#if UNITY_MARKDOWN_VIEWER
        EditorGUILayout.BeginHorizontal();
        ViewFile(sidebar, ref sidebarViewer, isSidebar: true);
        ViewFile(file, ref mainViewer, isSidebar: false);
        EditorGUILayout.EndHorizontal();
#endif

    }

#if UNITY_MARKDOWN_VIEWER

    const char ZeroWidthSpace = '​';

    readonly System.Collections.Generic.Dictionary<string, Vector2> scroll = new System.Collections.Generic.Dictionary<string, Vector2>();
    void ViewFile(string file, ref MG.MDV.MarkdownViewer viewer, bool isSidebar)
    {

        if (!scroll.ContainsKey(file))
            scroll.Add(file, Vector2.zero);

        var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(file);
        if (!asset)
            return;

        if (viewer == null)
            viewer = new MG.MDV.MarkdownViewer(
                skin: MG.MDV.Preferences.DarkSkin ? darkSkin : lightSkin,
                file,
                content: ProcessDocument(asset, isSidebar))
            { drawToolbar = false };

        const float sidebarWidth = 250;
        const float margin = 12;

        var width = isSidebar ? sidebarWidth : position.width - sidebarWidth;

        GUILayout.BeginArea(new Rect((isSidebar ? 0 : sidebarWidth) + margin, 0, width - margin, position.height));
        scroll[file] = GUILayout.BeginScrollView(scroll[file], alwaysShowHorizontal: false, alwaysShowVertical: true);

        viewer.Draw(width - GUI.skin.verticalScrollbar.fixedWidth - (margin * 2) - 44);

        GUILayout.EndScrollView();
        GUILayout.EndArea();

    }

    string ProcessDocument(TextAsset asset, bool isSidebar)
    {
        if (isSidebar) //Add home and some spacing
            return ZeroWidthSpace + @"\" + Environment.NewLine + "[Home](Home)" + Environment.NewLine + asset.text;
        else //Add name of current file as header
            return (asset.name.EndsWith("Home") ? ZeroWidthSpace.ToString() : "# " + ObjectNames.NicifyVariableName(asset.name)) + Environment.NewLine + Environment.NewLine + asset.text;
    }

#endif


    #endregion

}

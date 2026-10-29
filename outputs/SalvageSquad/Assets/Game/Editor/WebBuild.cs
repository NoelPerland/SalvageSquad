using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace SalvageSquad.Editor
{
    public static class WebBuild
    {
        public static void Build()
        {
            string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../web-build"));
            Directory.CreateDirectory(output);
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { SliceSetup.ScenePath, PrototypeSetup.ScenePath },
                locationPathName = output,
                target = BuildTarget.WebGL,
                options = BuildOptions.None
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new Exception("WebGL build failed: " + report.summary.result);
            string template = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../web-template/index.html"));
            if (File.Exists(template)) File.Copy(template, Path.Combine(output, "index.html"), true);
            Debug.Log("Salvage Squad WebGL build: " + output);
        }
    }
}

#if UNITY_EDITOR
using System;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace ColdBrewCase.EditorTools
{
    public static class PrepareAlphaWebTemplate
    {
        [MenuItem("Tools/Cold Brew Case/Prepare Checkpoint Web Template")]
        public static void PrepareCheckpoint() => Prepare(false);
        [MenuItem("Tools/Cold Brew Case/Prepare Alpha Web Template")]
        public static void PrepareAlpha() => Prepare(true);
        private static void Prepare(bool analytics)
        {
            string templateRoot = Path.Combine(EditorApplication.applicationContentsPath,
                "PlaybackEngines/WebGLSupport/BuildTools/WebGLTemplates");
            string installed = Path.Combine(templateRoot,"Base/Minimal/index.html");
            if (!File.Exists(installed)) installed = Path.Combine(templateRoot,"Minimal/index.html");
            if (!File.Exists(installed))
                throw new FileNotFoundException("Install this editor's Web Build Support module in Unity Hub, " +
                    "then retry. If your installation layout differs, manually copy its Minimal template.", installed);
            string templateName = analytics ? "ColdBrewAlpha" : "ColdBrewCheckpoint";
            string destination = "Assets/WebGLTemplates/" + templateName;
            Directory.CreateDirectory(destination);
            string sourceDirectory = Path.GetDirectoryName(installed);
            foreach (string file in Directory.GetFiles(sourceDirectory,"*",SearchOption.AllDirectories))
            {
                string extension = Path.GetExtension(file).ToLowerInvariant();
                if (file == installed || (extension != ".js" && extension != ".css" && extension != ".json")) continue;
                string relative = file.Substring(sourceDirectory.Length + 1);
                string target = Path.Combine(destination,relative);
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                File.Copy(file,target,true);
            }
            // Take the loader/config boilerplate from the installed 6000.3.22f1 editor.
            string index = File.ReadAllText(installed);
            // The game and wrapper use no image assets.
            index = Regex.Replace(index, @"<link[^>]*rel\s*=\s*[""'](?:shortcut\s+)?icon[""'][^>]*>", "",
                RegexOptions.IgnoreCase);
            const string additions = @"
<style>
  html, body { margin:0; width:100%; height:100%; background:#0b1015; }
  body { display:grid; place-items:center; }
  canvas { display:block; width:min(100vw,177.7778vh,1280px)!important; height:auto!important; aspect-ratio:16/9; }
</style>
";
            int head = index.IndexOf("</head>", StringComparison.OrdinalIgnoreCase);
            if (head < 0) throw new InvalidOperationException("Minimal template has no </head>. Copy/inject manually.");
            string scripts = analytics ? "\n<script src=\"analytics-bootstrap.js\"></script>\n" +
                "<script type=\"module\" src=\"analytics.js\"></script>\n" : "";
            index = index.Insert(head,additions + scripts);
            File.WriteAllText(Path.Combine(destination,"index.html"),index);
            if (analytics) foreach (string name in new[] {"analytics-bootstrap.js","analytics.js"})
            {
                string source = Path.Combine("Tools/Analytics",name);
                if (!File.Exists(source)) throw new FileNotFoundException("Copy the bundle's Tools folder into the project root.",source);
                File.Copy(source,Path.Combine(destination,name),true);
            }
            AssetDatabase.Refresh();
            Debug.Log(templateName + " template prepared. Select it under Player > Web > Resolution and Presentation. " +
                "After changing Tools/Analytics/analytics.js, run this command again before rebuilding.");
        }
    }
}
#endif

using ACTAP.Extensions;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace ACTAP
{
    public static class ModHelper
    {
        internal static readonly Dictionary<string, Sprite> Sprites = new();

        public static Assembly GetAssembly()
        {
            return Assembly.GetAssembly(typeof(Plugin));
        }

        public static Sprite GetSprite(string fileName)
        {
            if (Sprites.TryGetValue(fileName, out Sprite cached))
            {
                return cached;
            }

            Assembly mod = GetAssembly();
            byte[] resource = null;
            foreach (string e in mod.GetManifestResourceNames())
            {
                string[] resourcePath = e.Split('.');
                string resourceName = resourcePath[2];

                if (resourceName == fileName)
                {
                    resource = mod.GetManifestResourceStream(e).GetByteArray();
                    break;
                }
            }

            Sprite sprite = null;
            if (resource != null)
            {
                Texture2D texture = new(2, 2) { filterMode = FilterMode.Bilinear };
                ImageConversion.LoadImage(texture, resource);
                sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), 10.8f);
            }
            Sprites[fileName] = sprite;
            return sprite;
        }
        /*
        public static void Msg(string msg)
        {
            ModMain.logSource.LogInfo(msg);
        }*/
    }
}

using System.Linq;
using UnityEditor;
using UnityEngine;
public static class ShaderCheck
{
    public static object Run()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        return new[]{"TinyWorld/Toon","TinyWorld/Water","TinyWorld/CliffSky"}.Select(n=>{
            var s=Shader.Find(n);
            if(!s)return n+": missing";
            var msgs=ShaderUtil.GetShaderMessages(s);
            return n+": "+(msgs.Length==0?"ok":string.Join(" | ",msgs.Select(m=>m.severity+" "+m.message+" L"+m.line)));
        }).ToArray();
    }
}

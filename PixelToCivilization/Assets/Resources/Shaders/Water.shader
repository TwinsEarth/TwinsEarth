// 像素到文明 · 半透明水面（Unlit，内置/URP/团结引擎通用）
Shader "PxC/Water"
{
    Properties
    {
        _Color ("Color", Color) = (0.1,0.4,0.7,0.6)
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            fixed4 _Color;
            struct appdata { float4 vertex:POSITION; };
            struct v2f { float4 pos:SV_POSITION; };
            v2f vert(appdata v){ v2f o; o.pos=UnityObjectToClipPos(v.vertex); return o; }
            fixed4 frag(v2f i):SV_Target{ return _Color; }
            ENDCG
        }
    }
}

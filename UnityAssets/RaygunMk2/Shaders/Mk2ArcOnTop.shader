// The low lightning across the back of the Mark II: the same look as Legacy Shaders/Particles/Additive, but drawn over
// everything (ZTest Always). The game draws the first-person gun with its own squashed depth, so a line sitting on the
// gun's body tested behind it and never showed (user, 2026-11-16: the top sparks, out in the air, showed fine).
Shader "RaygunMk2/ArcOnTop"
{
    Properties
    {
        _TintColor ("Tint", Color) = (0.5, 0.5, 0.5, 0.5)
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "Queue" = "Transparent+100" "IgnoreProjector" = "True" "RenderType" = "Transparent" "PreviewType" = "Plane" }
        Blend SrcAlpha One
        ColorMask RGB
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _TintColor;

            struct appdata { float4 vertex : POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                return 2.0f * i.color * _TintColor * tex2D(_MainTex, i.uv);
            }
            ENDCG
        }
    }
}

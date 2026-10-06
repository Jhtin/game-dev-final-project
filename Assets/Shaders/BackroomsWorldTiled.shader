Shader "Backrooms/WorldTiled"
{
    Properties
    {
        _Color ("Tint Color", Color) = (1, 1, 1, 1)
        _MainTex ("Albedo (Diffuse)", 2D) = "white" {}
        _BumpMap ("Normal Map", 2D) = "bump" {}
        _TileMetersX ("Tile Width (Meters)", Float) = 2.5
        _TileMetersY ("Tile Height (Meters)", Float) = 2.5
        _Glossiness ("Smoothness", Range(0, 1)) = 0.12
        _Metallic ("Metallic", Range(0, 1)) = 0.0
        [Toggle] _UseEmission ("Enable Emission", Float) = 0
        _EmissionMap ("Emission Map", 2D) = "black" {}
        [HDR] _EmissionColor ("Emission Color", Color) = (0, 0, 0, 1)
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows vertex:vert
        #pragma target 3.0

        sampler2D _MainTex;
        sampler2D _BumpMap;
        sampler2D _EmissionMap;

        fixed4 _Color;
        half _Glossiness;
        half _Metallic;
        float _TileMetersX;
        float _TileMetersY;
        float _UseEmission;
        fixed4 _EmissionColor;

        struct Input
        {
            float2 worldUV;
        };

        void vert (inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
            float3 worldNormal = UnityObjectToWorldNormal(v.normal);
            float3 n = abs(worldNormal);

            float scaleX = max(0.1, _TileMetersX);
            float scaleY = max(0.1, _TileMetersY);

            if (n.y > 0.5)
            {
                // Floor / Ceiling: project onto XZ plane
                o.worldUV = float2(worldPos.x / scaleX, worldPos.z / scaleY);
            }
            else if (n.x > 0.5)
            {
                // East / West Wall: project onto ZY plane
                o.worldUV = float2(worldPos.z / scaleX, worldPos.y / scaleY);
            }
            else
            {
                // North / South Wall: project onto XY plane
                o.worldUV = float2(worldPos.x / scaleX, worldPos.y / scaleY);
            }
        }

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 col = tex2D(_MainTex, IN.worldUV) * _Color;
            o.Albedo = col.rgb;
            o.Normal = UnpackNormal(tex2D(_BumpMap, IN.worldUV));
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            o.Alpha = col.a;

            if (_UseEmission > 0.5)
            {
                fixed4 emissive = tex2D(_EmissionMap, IN.worldUV) * _EmissionColor;
                o.Emission = emissive.rgb;
            }
        }
        ENDCG
    }
    FallBack "Standard"
}

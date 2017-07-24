// Upgrade NOTE: upgraded instancing buffer 'FishInstanceProperties' to new syntax.

Shader "Custom/WiggleInstancing" {
	Properties {
		_MainTex ("Albedo (RGBA)", 2D) = "white" {}
        _Emission ("Emission (RGBA)", 2D) = "black" {}
        _AoMap ("AoMap (RGBA)", 2D) = "white" {}
        _NormalMap ("Normal Map", 2D) = "Normal" {}
		_Gloss ("_MetallicGloss (RGB)", 2D) = "white" {}
		_Tints ("Tints (RGB)", 2D) = "white" {}
		_Glossiness ("Smoothness", Range(0,1)) = 0.5
		_Metallic ("Metallic", Range(0,1)) = 0.0
		_Amount ("Wave1 Frequency", float) = 1
		_TimeScale ("Wave1 Speed", float) = 1.0
		_Distance ("Distance", float) = 0.1
        
	}
	SubShader {
		Tags { "RenderType"="Opaque" }
		LOD 200
		
		CGPROGRAM
		#pragma surface surf Standard fullforwardshadows  vertex:vert addshadow
		#pragma target 3.0
		#pragma multi_compile_instancing
		#pragma instancing_options procedural:setup

		sampler2D _MainTex;
        sampler2D _Emission;
        sampler2D _AoMap;
        sampler2D _NormalMap;
		sampler2D _Tints;
		sampler2D _Gloss;

		struct Input {
			float2 uv_MainTex;
		};

		half4 _Direction;
		half _Glossiness;
		half _Metallic;
        fixed4 _Color;
		float _TimeScale;
		float _Amount;
		float _Distance;

        UNITY_INSTANCING_BUFFER_START (FishInstanceProperties)
        	UNITY_DEFINE_INSTANCED_PROP (float, _InstanceCycleOffset)
#define _InstanceCycleOffset_arr FishInstanceProperties
        UNITY_INSTANCING_BUFFER_END(FishInstanceProperties)

        #ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
        // @TODO: workaround for a strange bug with missing StructuredBuffer and no SHADER_API set when compiling
        #if defined(SHADER_API_DESKTOP) || defined(SHADER_API_MOBILE)
       	StructuredBuffer<float4x4> matrixBuffer;
        #endif
    	#endif

    	void setup()
        {
        #ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
        // @TODO: workaround for a strange bug with missing StructuredBuffer and no SHADER_API set when compiling
        #if defined(SHADER_API_DESKTOP) || defined(SHADER_API_MOBILE)
            unity_ObjectToWorld = matrixBuffer[unity_InstanceID];
            unity_WorldToObject = unity_ObjectToWorld;
            unity_WorldToObject._14_24_34 *= -1;
            unity_WorldToObject._11_22_33 = 1.0f / unity_WorldToObject._11_22_33;
        #endif
        #endif
        }

		void vert(inout appdata_full v)
		{
			float cycleOffset = UNITY_ACCESS_INSTANCED_PROP(_InstanceCycleOffset_arr, _InstanceCycleOffset);
			float4 offs_tail = (sin((cycleOffset + _Time.y) * _TimeScale + v.vertex.z * _Amount) * _Distance)*v.color.r;
            float4 offs_tentacles = (sin((cycleOffset + _Time.y) * _TimeScale + v.vertex.z * _Amount) * _Distance) * v.color.b;
            float4 finaloffs_tail = float4(offs_tail.x , offs_tail.y * v.color.g, offs_tail.z, offs_tail.w);
            float4 finaloffs_tentacles = float4(offs_tentacles.x, offs_tentacles.y, offs_tentacles.z, offs_tentacles.w);
            v.vertex.x += finaloffs_tail;
            v.vertex.y += finaloffs_tentacles;
            
    	}

		void surf (Input IN, inout SurfaceOutputStandard o)
		{
			fixed4 c = tex2D (_MainTex, IN.uv_MainTex);
			fixed4 g = tex2D (_Gloss, IN.uv_MainTex);
            fixed4 e = tex2D (_Emission, IN.uv_MainTex);
			o.Albedo = c*2;
            o.Metallic = g;
			o.Smoothness = g.a * _Glossiness;
            o.Normal = UnpackNormal(tex2D(_NormalMap, IN.uv_MainTex));
            o.Emission = e*50;
            
            //o.Alpha = c.a;
		}
		ENDCG
	}
	FallBack "Diffuse"
}

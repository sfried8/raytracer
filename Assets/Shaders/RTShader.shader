Shader "Custom/RTShader"
{
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };
            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            /////////////////
            // STRUCTS
            /////////////////
            const int CheckerPattern = 1;


            /////////////////
            // STRUCTS
            /////////////////

            struct Ray {
                float3 origin;
                float3 direction;
            };

            struct RTMaterial {
                float4 color;
                float4 emissionColor;
                float4 specularColor;
                float emissionStrength;
                float smoothness;
                float specularProbability;
                int flag;
                float4 checkerColor2;
                float checkerScale;
                float invCheckerScale;
            };

            struct Sphere {
                float3 origin;
                float radius;
                RTMaterial material;
            };

            struct HitInfo {
                bool did_hit;
                float3 hit_point;
                float dist;
                float3 normal;
                RTMaterial material;
                bool err;
            };

            struct Triangle
            {
                float3 Q;
                float3 u;
                float3 v;
                float3 n;
                float D;
                float3 w;
                float3 normal;
            };
            struct MeshInfo
            {
                int numTriangles;
                int triangleStartIndex;
                float3 boundsMin;
                float3 boundsMax;
                RTMaterial material;
            };
            /////////////////
            // VARIABLES
            /////////////////
            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4x4 CamLocalToWorldMatrix;
            float3 ViewParams;
            StructuredBuffer<Sphere> Spheres;
            StructuredBuffer<Triangle> Triangles;
            StructuredBuffer<MeshInfo> Meshes;
            int Frame;
            int NumSpheres;
            int NumTriangles;
            int NumMeshes;
            int MaxBounces;
            int RaysPerPixel;

            /////////////////
            // RNG
            /////////////////
            uint NextRandom(inout uint state)
            {
                
                state = state * 747796405 + 2891336453;
                uint result = ((state >> ((state >> 28) + 4)) ^ state) * 277803737;
                result = (result >> 22) ^ result;
                return result;
                
            } 

            float RandomValue(inout uint state)
            {
                return NextRandom(state) / 4294967295.0; // 2^32 - 1
            }
            float2 RandomPointInSquare(inout uint state) {
                float x = RandomValue(state);
                float y = RandomValue(state);
                return float2(x - 0.5, y-0.5);
            }
            float3 RandomPointInSphere(inout uint state)
            {
                float x;
                float y;
                float z;
                for (int i = 0; i < 100; i++) {

                    x = RandomValue(state) - 0.5;
                    y = RandomValue(state) - 0.5;
                    z = RandomValue(state) - 0.5;
                    if (x*x+y*y+z*z <= 0.25) {
                        break;
                    }
                }
                return normalize(float3(x,y,z));
            }

            /////////////////
            // HELPERS
            /////////////////
            float3 at(Ray r, float t) {
                return r.origin + r.direction*t;
            }


            /////////////////
            // HIT LOGIC
            /////////////////
            HitInfo hit_sphere(float3 sphereOrigin, float sphereRadius, Ray r) {
                HitInfo hit = (HitInfo)0;
                float3 oc = sphereOrigin - r.origin;
                float a = dot(r.direction, r.direction);
                float b = -2.0 * dot(r.direction, oc);
                float c = dot(oc, oc) - sphereRadius * sphereRadius;
                float discriminant = b*b - 4.0*a*c;
                if (discriminant < 0.0) 
                {
                    hit.did_hit = false;
                    } else {
                    hit.dist = (-b - sqrt(discriminant))/(2.0*a); 
                    if (hit.dist >= 0.0001) {

                        hit.did_hit = true;
                        hit.hit_point = at(r, hit.dist);
                        hit.normal = normalize(hit.hit_point - sphereOrigin);
                    }
                }
                return hit;
            }
            HitInfo hit_triangle(Triangle tri, Ray r) {
                HitInfo hit = (HitInfo)0;
                tri.n = cross(tri.u, tri.v);
                tri.normal = normalize(tri.n);
                tri.D = dot(tri.normal, tri.Q);
                tri.w = tri.n / dot(tri.n, tri.n);
                float denom = dot(tri.normal, r.direction);
                if (denom > -0.00001) {
                    return hit;
                }
                float t = (tri.D - dot(tri.normal, r.origin))/denom;
                if (t < 0.001) 
                {
                    hit.did_hit = false;
                    } else {
                    float3 intersection = at(r, t);
                    float3 planar_hitpt_vector = intersection - tri.Q;
                    float alpha = dot(tri.w, cross(planar_hitpt_vector, tri.v));
                    float beta = dot(tri.w, cross(tri.u, planar_hitpt_vector));
                    if (alpha > 0.0 && beta > 0.0 && alpha + beta < 1.0) {
                        hit.did_hit = true;
                        hit.dist = t;
                        hit.hit_point = intersection;
                        hit.normal = tri.normal;
                    }
                }
                return hit;
            }
            bool hit_aabb(float3 boundsMin, float3 boundsMax, Ray r) {
                // r.dir is unit direction vector of ray
                float dirfracx = 1.0f / r.direction.x;
                float dirfracy = 1.0f / r.direction.y;
                float dirfracz = 1.0f / r.direction.z;
                // lb is the corner of AABB with minimal coordinates - left bottom, rt is maximal corner
                // r.org is origin of ray
                float t1 = (boundsMin.x - r.origin.x)*dirfracx;
                float t2 = (boundsMax.x - r.origin.x)*dirfracx;
                float t3 = (boundsMin.y - r.origin.y)*dirfracy;
                float t4 = (boundsMax.y - r.origin.y)*dirfracy;
                float t5 = (boundsMin.z - r.origin.z)*dirfracz;
                float t6 = (boundsMax.z - r.origin.z)*dirfracz;

                float tmin = max(max(min(t1, t2), min(t3, t4)), min(t5, t6));
                float tmax = min(min(max(t1, t2), max(t3, t4)), max(t5, t6));

                // if tmax < 0, ray (line) is intersecting AABB, but the whole AABB is behind us
                if (tmax < 0)
                {
                    return false;
                }

                // if tmin > tmax, ray doesn't intersect AABB
                if (tmin > tmax)
                {
                    return false;
                }
                return true;
            }

            HitInfo hit_mesh(MeshInfo mesh, Ray r) {
                HitInfo closestHit = (HitInfo)0;
                closestHit.dist = 1.#INF;
                closestHit.material = mesh.material;
                if (!hit_aabb(mesh.boundsMin, mesh.boundsMax, r)) {
                    // closestHit.err = true;
                    return closestHit;
                }
                HitInfo hit = (HitInfo)0;
                for (int i = 0; i < mesh.numTriangles; i++) {
                    Triangle tri = Triangles[mesh.triangleStartIndex + i];
                    hit = hit_triangle(tri, r);
                    if (hit.did_hit) {
                        if (hit.dist < closestHit.dist) {
                            closestHit = hit;
                        }
                    }
                }
                return closestHit;
            }
            float4 get_material_color(HitInfo hit) {
                if (hit.material.flag | CheckerPattern) {
                    int x = (int)floor(hit.hit_point.x * hit.material.invCheckerScale);
                    int y = (int)floor(hit.hit_point.y * hit.material.invCheckerScale);
                    int z = (int)floor(hit.hit_point.z * hit.material.invCheckerScale);
                    if ((x + y + z) % 2 == 0) {
                        return hit.material.checkerColor2;
                    }
                }
                return hit.material.color;

            }
            float4 ray_color(Ray r, inout uint rng) {
                HitInfo closestHit;
                float3 currentRayColor = float3(1,1,1);
                float3 light = float3(0,0,0);
                for(int bounce = 0; bounce < MaxBounces; bounce++) {

                    closestHit.dist = 1.#INF;
                    closestHit.did_hit = false;
                    for (int i = 0; i < NumSpheres; i++){
                        Sphere s = Spheres[i];
                        HitInfo hit = hit_sphere(s.origin, s.radius, r);
                        if (hit.did_hit) {
                            if (hit.dist < closestHit.dist) {
                                closestHit = hit;
                                closestHit.material = s.material;
                            }
                        }
                    }
                    for (int i = 0; i < NumMeshes; i++){
                        MeshInfo mesh = Meshes[i];
                        HitInfo hit = hit_mesh(mesh, r);
                        if (hit.err) {
                            return float4(1,0,0,1);
                        }
                        if (hit.did_hit) {
                            if (hit.dist < closestHit.dist) {
                                closestHit = hit;
                                closestHit.material = mesh.material;
                            }
                        }
                    }
                    if (!closestHit.did_hit) {
                        light += currentRayColor * float3(0,0.14,0.74);
                        break;
                    }
                    bool isSpecular = RandomValue(rng) <= closestHit.material.specularProbability;
                    currentRayColor *= isSpecular ? closestHit.material.specularColor : get_material_color(closestHit);
                    light += currentRayColor * closestHit.material.emissionColor * closestHit.material.emissionStrength;
                    float3 specularDir = reflect(r.direction, closestHit.normal);
                    r = (Ray)0;
                    r.origin = closestHit.hit_point;
                    float3 scatterDir = closestHit.normal + RandomPointInSphere(rng);
                    if(dot(closestHit.normal, scatterDir) < 0.0) {
                        scatterDir = -scatterDir;
                    }
                    r.direction = normalize(lerp(scatterDir, specularDir, isSpecular ? closestHit.material.smoothness : 0));
                }
                return float4(light, 1);
                


            }

            float4 displayFloat(float f) {
                if (f > 1.0) {
                    return float4(1,0,0,1);
                    } else if (f < 0.0) {
                    return float4(0,1,0,1);
                }
                return float4(f,f,f,1);
            }

            fixed4 frag (v2f i) : SV_Target
            {
                uint2 numPixels = _ScreenParams.xy;
                uint2 pixelCoord = i.uv * numPixels;
                uint pixelIndex = pixelCoord.y * numPixels.x + pixelCoord.x;
                uint rngState = pixelIndex + Frame * 719393;
                // return float4(RandomPointInSphere(rngState), 1);

                float3 viewPointLocal = float3(i.uv - 0.5, 1) * ViewParams;
                float3 viewPoint = mul(CamLocalToWorldMatrix, float4(viewPointLocal, 1));
                float4 pixelcolor = float4(0,0,0,0);
                for (int j = 0; j < RaysPerPixel; j++) {

                    Ray r;
                    r.origin = _WorldSpaceCameraPos;
                    float2 offset = RandomPointInSquare(rngState);
                    r.direction = normalize((viewPoint - _WorldSpaceCameraPos)) + float3(offset.x/numPixels.x, offset.y/numPixels.y, 0);
                    pixelcolor += ray_color(r, rngState)/float(RaysPerPixel);
                }

                return pixelcolor;
            }
            ENDCG
        }
    }
}

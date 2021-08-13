using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DrawMeshInstancedIndirect : MonoBehaviour
{
    public int population;
    public float range;
    public bool isControl;
    public float ControlRadius;

    public Material material;
    public ComputeShader compute;
    public Transform pusher;
    MeshCollider meshC;
    Mesh meshCombine;

    private ComputeBuffer meshPropertiesBuffer;
    private ComputeBuffer argsBuffer;

    CombineInstance[] cI;
    MeshProperties[] properties;

    public Mesh mesh;

    // Mesh Properties struct to be read from the GPU.
    // Size() is a convenience funciton which returns the stride of the struct.
    private struct MeshProperties
    {
        public Matrix4x4 mat;
        public Vector4 color;

        public static int Size()
        {
            return
                sizeof(float) * 4 * 4 + // matrix;
                sizeof(float) * 4; // color;      
        }
    }



    private void Setup()
    {
        meshC = GetComponent<MeshCollider>();
        
        cI = new CombineInstance[population];

        InitializeBuffers();
    }

    private void InitializeBuffers()
    {
        int kernel = compute.FindKernel("CSMain");

        // Argument buffer used by DrawMeshInstancedIndirect.
        uint[] args = new uint[5] { 0, 0, 0, 0, 0 };
        // Arguments for drawing mesh.
        // 0 == number of triangle indices, 1 == population, others are only relevant if drawing submeshes.
        args[0] = (uint)mesh.GetIndexCount(0);
        args[1] = (uint)population;
        args[2] = (uint)mesh.GetIndexStart(0);
        args[3] = (uint)mesh.GetBaseVertex(0);

        argsBuffer = new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
        argsBuffer.SetData(args);

        // Initialize buffer with the given population.
        properties = new MeshProperties[population];
        meshCombine = new Mesh();

        for (int i = 0; i < population; i++)
        {
            MeshProperties props = new MeshProperties();
            Vector3 position = new Vector3(Random.Range(-range, range), Random.Range(-range, range), Random.Range(-range, range));
            Quaternion rotation = Quaternion.Euler(Random.Range(-180, 180), Random.Range(-180, 180), Random.Range(-180, 180));
            Vector3 scale = Vector3.one;

            props.mat = Matrix4x4.TRS(position, rotation, scale);
            props.color = Color.Lerp(Color.red, Color.blue, Random.value);

            cI[i].mesh = mesh;
            cI[i].transform = Matrix4x4.TRS(position, rotation, scale);

            properties[i] = props;
        }
        CreateColloder();

        meshPropertiesBuffer = new ComputeBuffer(population, MeshProperties.Size());
        meshPropertiesBuffer.SetData(properties);
        compute.SetBuffer(kernel, "_Properties", meshPropertiesBuffer);
        material.SetBuffer("_Properties", meshPropertiesBuffer);
    }

    private void Start()
    {
        Setup();
    }


    private void Update()
    {
        if (isControl && Input.GetMouseButtonDown(1))
        {
            int kernel = compute.FindKernel("CSMain");
            compute.SetFloat("_dis", ControlRadius);
            compute.SetVector("_PusherPosition", pusher.position);
            compute.Dispatch(kernel, Mathf.CeilToInt(population / 64f), 1, 1);
            MeshProperties[] pp = new MeshProperties[population];
            meshPropertiesBuffer.GetData(pp);
            for (int i =0;i<pp.Length;i++)
            {
                cI[i].transform = pp[i].mat;
            }
            CreateColloder();
        }

        Graphics.DrawMeshInstancedIndirect(mesh, 0, material, meshCombine.bounds, argsBuffer, 0,null,UnityEngine.Rendering.ShadowCastingMode.On,true);
    }

    private void CreateColloder()
    {
        meshCombine.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        meshCombine.CombineMeshes(cI,true);
        meshC.sharedMesh = meshCombine;
    }

    private void OnDisable()
    {
        // Release gracefully.
        if (meshPropertiesBuffer != null)
        {
            meshPropertiesBuffer.Release();
        }
        meshPropertiesBuffer = null;

        if (argsBuffer != null)
        {
            argsBuffer.Release();
        }
        argsBuffer = null;
    }
}

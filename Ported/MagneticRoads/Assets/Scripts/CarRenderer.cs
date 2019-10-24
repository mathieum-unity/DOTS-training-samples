using System;
using System.Collections.Generic;
using System.Runtime.InteropServices.WindowsRuntime;
using Unity.Collections;
using Unity.Transforms;
using UnityEngine;
using Random = UnityEngine.Random;

public class CarRenderer : MonoBehaviour
{
    [SerializeField]
    Mesh m_Mesh;
    
    [SerializeField]
    Material m_Material;

    const int k_BlockSize = 1023;

    struct DrawInstancedArgs
    {
        public MaterialPropertyBlock properties;
        public Matrix4x4[] transforms;
        public Vector4[] colors;
        public int size;
    }

    static List<CarRenderer> s_Instances = new List<CarRenderer>();
    
    public static CarRenderer GetInstance()
    {
        if (s_Instances.Count == 0)
            return null;
        return s_Instances[0];
    }

    void Awake() { s_Instances.Add(this); }

    void OnDestroy() { s_Instances.Remove(this); }

    Stack<DrawInstancedArgs> m_ArgsPool = new Stack<DrawInstancedArgs>();
    List<DrawInstancedArgs> m_CurrentArgs = new List<DrawInstancedArgs>();

    DrawInstancedArgs CreateArgs()
    {
        if (m_ArgsPool.Count > 1)
        {
            var args = m_ArgsPool.Pop();
            args.size = 0;
            return args;
        }

        return new DrawInstancedArgs
        {
            size = 0,
            transforms = new Matrix4x4[k_BlockSize],
            colors = new Vector4[k_BlockSize],
            properties = new MaterialPropertyBlock()
        };
    }

    public class WriteAccess
    {
        CarRenderer m_Renderer;
        List<DrawInstancedArgs> m_Args = new List<DrawInstancedArgs>();

        public WriteAccess(CarRenderer renderer) { m_Renderer = renderer; }

        public void Reset()
        {
            // recycle current args
            foreach (var args in m_Args)     
                m_Renderer.m_ArgsPool.Push(args);
            // add range expects m_Args to not be empty
            m_Args.Add(m_Renderer.CreateArgs());
        }

        public void AddRange(NativeArray<LocalToWorld> transforms, NativeArray<ColorData> colors)
        {
            var inputIndex = 0;
            var inputSize = transforms.Length;
            for (;;)
            {
                var currentArgs = m_Args[m_Args.Count - 1];
                var capacity = k_BlockSize - currentArgs.size;
                var remainingInput = inputSize - inputIndex;
                var count = (int)Mathf.Min(capacity, remainingInput);
                
                for (var i = 0; i != count; ++i)
                {
                    currentArgs.transforms[currentArgs.size + i] = transforms[inputIndex].Value;
                    currentArgs.colors[currentArgs.size + i] = colors[inputIndex].value;
                    ++inputIndex;
                }

                currentArgs.size += count;
                m_Args[m_Args.Count - 1] = currentArgs;
                
                // added all remaining elements, done
                if (count == remainingInput)
                    break;

                m_Args.Add(m_Renderer.CreateArgs());
            }
        }

        public void Apply()
        {
            // was already applied
            if (m_Args.Count == 0)
                return;
            
            // recycle currently used blocks
            foreach (var args in m_Renderer.m_CurrentArgs)     
                m_Renderer.m_ArgsPool.Push(args);           
            m_Renderer.m_CurrentArgs.Clear();
            
            // update material properties
            foreach (var args in m_Args)
                args.properties.SetVectorArray("_Color", args.colors);
            
            // update currently used blocks
            m_Renderer.m_CurrentArgs.AddRange(m_Args);
            m_Args.Clear();
        }
    }

    WriteAccess m_WriteAccess = null;

    public WriteAccess GetWriteAccess()
    {
        if (m_WriteAccess == null)
            m_WriteAccess = new WriteAccess(this);
        return m_WriteAccess;
    }

    void Update()
    {
        foreach (var args in m_CurrentArgs)
            Graphics.DrawMeshInstanced(m_Mesh, 0, m_Material, args.transforms, args.size, args.properties);
    }
}


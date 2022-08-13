using System;
using System.Collections.Generic;
using Assimp;
using System.Text;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.OpenGL;
using System.Numerics;
using static Avalonia.OpenGL.GlConsts;
using System.Runtime.InteropServices;
using System.Diagnostics;
using Avalonia;
using Avalonia.Input;

namespace Avalonia3DModelRenderer
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            mBrowseButton.Click += BrowseButton_Click;
        }

        async void BrowseButton_Click(object? sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog();
            dialog.Title = "Select a 3D model file";
            dialog.Filters.Add(new FileDialogFilter
            {
                Name = "3D model files",
                //Extensions = new List<string> { "obj", "3ds", "dae", "stl", "ply" }
            });

            string[]? selectedFiles = await dialog.ShowAsync(this);

            if (selectedFiles == null || selectedFiles.Length == 0)
                return;

            string fileName = selectedFiles[0];

            LoadProperties(fileName);
        }

        void LoadProperties(string fileName)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("File name: " + fileName);

            using (var imp = new AssimpContext())
            {
                Scene rawScene = imp.ImportFile(fileName);

                if (rawScene == null)
                {
                    sb.AppendLine("Error: Failed to load the file.");
                    return;
                }

                sb.AppendLine(rawScene.AnimationCount + " animations");
                sb.AppendLine(rawScene.MeshCount + " meshes");
                sb.AppendLine(rawScene.MaterialCount + " materials");
                sb.AppendLine(rawScene.TextureCount + " textures");
                sb.AppendLine(rawScene.LightCount + " lights");
                sb.AppendLine(rawScene.CameraCount + " cameras");
                sb.AppendLine(rawScene.SceneFlags + " scene flags");
                WriteMetadata(rawScene.Metadata, sb);

                mOpenGlControl = new OpenGlControl(rawScene);
                mOpenGlControlContainer.Child = mOpenGlControl;

                mContextTextBox.Text = sb.ToString();
            }
        }

        private void WriteMetadata(Metadata metadata, StringBuilder sb)
        {
            if (metadata == null)
                return;

            foreach (var key in metadata.Keys)
            {
                sb.AppendLine(key + ": " + metadata[key]);
            }
        }

        OpenGlControl mOpenGlControl;
    }

    public class OpenGlControl : Avalonia.OpenGL.Controls.OpenGlControlBase
    {

private float _yaw;

        public static readonly DirectProperty<OpenGlControl, float> YawProperty =
            AvaloniaProperty.RegisterDirect<OpenGlControl, float>("Yaw", o => o.Yaw, (o, v) => o.Yaw = v);

        public float Yaw
        {
            get => _yaw;
            set => SetAndRaise(YawProperty, ref _yaw, value);
        }

        private Vector3 _cameraPosition;

        public static readonly DirectProperty<OpenGlControl, Vector3> CameraPositionProperty =
            AvaloniaProperty.RegisterDirect<OpenGlControl, Vector3>("CameraPosition", o => o.CameraPosition, (o, v) => o.CameraPosition = v);

        public Vector3 CameraPosition
        {
            get => _cameraPosition;
            set => SetAndRaise(CameraPositionProperty, ref _cameraPosition, value);
        }

        private float _pitch;

        public static readonly DirectProperty<OpenGlControl, float> PitchProperty =
            AvaloniaProperty.RegisterDirect<OpenGlControl, float>("Pitch", o => o.Pitch, (o, v) => o.Pitch = v);

        public float Pitch
        {
            get => _pitch;
            set => SetAndRaise(PitchProperty, ref _pitch, value);
        }


        private float _roll;

        public static readonly DirectProperty<OpenGlControl, float> RollProperty =
            AvaloniaProperty.RegisterDirect<OpenGlControl, float>("Roll", o => o.Roll, (o, v) => o.Roll = v);

        public float Roll
        {
            get => _roll;
            set => SetAndRaise(RollProperty, ref _roll, value);
        }


        private float _disco;

        public static readonly DirectProperty<OpenGlControl, float> DiscoProperty =
            AvaloniaProperty.RegisterDirect<OpenGlControl, float>("Disco", o => o.Disco, (o, v) => o.Disco = v);

        public float Disco
        {
            get => _disco;
            set => SetAndRaise(DiscoProperty, ref _disco, value);
        }

        static OpenGlControl()
        {
            AffectsRender<OpenGlControl>(YawProperty, PitchProperty, RollProperty, DiscoProperty, CameraPositionProperty);
        }

        string GetShader(bool fragment, string shader)
        {
            var version = (GlVersion.Type == GlProfileType.OpenGL ?
                RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? 150 : 120 :
                100);
            var data = "#version " + version + "\n";
            if (GlVersion.Type == GlProfileType.OpenGLES)
                data += "precision mediump float;\n";
            if (version >= 150)
            {
                shader = shader.Replace("attribute", "in");
                if (fragment)
                    shader = shader
                        .Replace("varying", "in")
                        .Replace("//DECLAREGLFRAG", "out vec4 outFragColor;")
                        .Replace("gl_FragColor", "outFragColor");
                else
                    shader = shader.Replace("varying", "out");
            }

            data += shader;

            return data;
        }


        string VertexShaderSource => GetShader(false, @"
        attribute vec3 aPos;
        attribute vec3 aNormal;
        uniform mat4 uModel;
        uniform mat4 uProjection;
        uniform mat4 uView;

        varying vec3 FragPos;
        varying vec3 VecPos;  
        varying vec3 Normal;
        uniform float uTime;
        uniform float uDisco;
        void main()
        {
            float discoScale = sin(uTime * 10.0) / 10.0;
            float distortionX = 1.0 + uDisco * cos(uTime * 20.0) / 10.0;
            
            float scale = 1.0 + uDisco * discoScale;
            
            vec3 scaledPos = aPos;
            scaledPos.x = scaledPos.x * distortionX;
            
            scaledPos *= scale;
            gl_Position = uProjection * uView * uModel * vec4(scaledPos, 1.0);
            FragPos = vec3(uModel * vec4(aPos, 1.0));
            VecPos = aPos;
            Normal = normalize(vec3(uModel * vec4(aNormal, 1.0)));
        }
");

        private string FragmentShaderSource => GetShader(true, @"
        varying vec3 FragPos; 
        varying vec3 VecPos; 
        varying vec3 Normal;
        uniform float uMaxY;
        uniform float uMinY;
        uniform float uTime;
        uniform float uDisco;
        //DECLAREGLFRAG

        void main()
        {
            float y = (VecPos.y - uMinY) / (uMaxY - uMinY);
            float c = cos(atan(VecPos.x, VecPos.z) * 20.0 + uTime * 40.0 + y * 50.0);
            float s = sin(-atan(VecPos.z, VecPos.x) * 20.0 - uTime * 20.0 - y * 30.0);

            vec3 discoColor = vec3(
                0.5 + abs(0.5 - y) * cos(uTime * 10.0),
                0.25 + (smoothstep(0.3, 0.8, y) * (0.5 - c / 4.0)),
                0.25 + abs((smoothstep(0.1, 0.4, y) * (0.5 - s / 4.0))));

            vec3 objectColor = vec3((1.0 - y), 0.40 +  y / 4.0, y * 0.75 + 0.25);
            objectColor = objectColor * (1.0 - uDisco) + discoColor * uDisco;

            float ambientStrength = 0.3;
            vec3 lightColor = vec3(1.0, 1.0, 1.0);
            vec3 lightPos = vec3(uMaxY * 2.0, uMaxY * 2.0, uMaxY * 2.0);
            vec3 ambient = ambientStrength * lightColor;


            vec3 norm = normalize(Normal);
            vec3 lightDir = normalize(lightPos - FragPos);  

            float diff = max(dot(norm, lightDir), 0.0);
            vec3 diffuse = diff * lightColor;

            vec3 result = (ambient + diffuse) * objectColor;
            gl_FragColor = vec4(result, 1.0);

        }
");

        public OpenGlControl(Scene scene)
        {
            mScene = scene;
            CameraPosition = new Vector3(25, 25, 25);
        }

        protected unsafe override void OnOpenGlInit(GlInterface GL, int fb)
        {
            base.OnOpenGlInit(GL, fb);

            RecursiveLoadScene(mScene.RootNode, mScene);

            CheckError(GL);
            _glExt = new GlExtrasInterface(GL);
           
            // Load the source of the vertex shader and compile it.
            _vertexShader = GL.CreateShader(GL_VERTEX_SHADER);
            Console.WriteLine(GL.CompileShaderAndGetError(_vertexShader, VertexShaderSource));

            // Load the source of the fragment shader and compile it.
            _fragmentShader = GL.CreateShader(GL_FRAGMENT_SHADER);
            Console.WriteLine(GL.CompileShaderAndGetError(_fragmentShader, FragmentShaderSource));

            // Create the shader program, attach the vertex and fragment shaders and link the program.
            _shaderProgram = GL.CreateProgram();
            GL.AttachShader(_shaderProgram, _vertexShader);
            GL.AttachShader(_shaderProgram, _fragmentShader);
            const int positionLocation = 0;
            const int normalLocation = 1;
            GL.BindAttribLocationString(_shaderProgram, positionLocation, "aPos");
            GL.BindAttribLocationString(_shaderProgram, normalLocation, "aNormal");
            Console.WriteLine(GL.LinkProgramAndGetError(_shaderProgram));
            CheckError(GL);

            // Create the vertex buffer object (VBO) for the vertex data.
            _vertexBufferObject = GL.GenBuffer();
            // Bind the VBO and copy the vertex data into it.
            GL.BindBuffer(GL_ARRAY_BUFFER, _vertexBufferObject);
            CheckError(GL);
            var vertexSize = Marshal.SizeOf<Vertex>();
            fixed (void* pdata = _points.ToArray())
                GL.BufferData(GL_ARRAY_BUFFER, new IntPtr(_points.Count * vertexSize),
                    new IntPtr(pdata), GL_STATIC_DRAW);

            _facesBufferObject = GL.GenBuffer();
            GL.BindBuffer(GL_ELEMENT_ARRAY_BUFFER, _facesBufferObject);
            CheckError(GL);
            fixed (void* pdata1 = _faces.ToArray())
                GL.BufferData(GL_ELEMENT_ARRAY_BUFFER, new IntPtr(_faces.Count * sizeof(Triangle)), new IntPtr(pdata1),
                    GL_STATIC_DRAW);
            CheckError(GL);
            _vertexArrayObject = _glExt.GenVertexArray();
            _glExt.BindVertexArray(_vertexArrayObject);
            CheckError(GL);
            GL.VertexAttribPointer(positionLocation, 3, GL_FLOAT,
                0, vertexSize, IntPtr.Zero);
            GL.VertexAttribPointer(normalLocation, 3, GL_FLOAT,
                0, vertexSize, new IntPtr(12));
            GL.EnableVertexAttribArray(positionLocation);
            GL.EnableVertexAttribArray(normalLocation);
            CheckError(GL);
        }

        protected override void OnOpenGlDeinit(GlInterface gl, int fb)
        {
            base.OnOpenGlDeinit(gl, fb);
        }

        protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
        {
            base.OnPointerWheelChanged(e);

            if (e.KeyModifiers == KeyModifiers.Control)
            {
                CameraPosition = new Vector3(
                    CameraPosition.X - (float)e.Delta.Y * 10,
                    CameraPosition.Y,
                    CameraPosition.Z);
                return;
            }

            if (e.KeyModifiers == KeyModifiers.Shift)
        {
                CameraPosition = new Vector3(
                    CameraPosition.X,
                    CameraPosition.Y - (float)e.Delta.Y * 10,
                    CameraPosition.Z);
                    return;
            }

            CameraPosition = new Vector3(
                CameraPosition.X,
                CameraPosition.Y,
                CameraPosition.Z - (float)e.Delta.Y * 10);
        }

        bool mIsDragging = false;

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);
            mIsDragging = true;
        }

        protected override void OnPointerReleased(PointerReleasedEventArgs e)
        {
            base.OnPointerReleased(e);
            mIsDragging = false;
        }

        protected override void OnPointerMoved(PointerEventArgs e)
        {
            base.OnPointerMoved(e);

            if (!mIsDragging)
                return;

            if (e.KeyModifiers == KeyModifiers.Control)
            {
                Pitch = (float)e.GetPosition(this).X.Map(0, Bounds.Width, 0, 8);
                return;
            }

            Yaw = (float)e.GetPosition(this).X.Map(0, Bounds.Width, 0, 8);
            Roll = (float)e.GetPosition(this).Y.Map(0, Bounds.Height, 0, 8);
        }

        protected unsafe override void OnOpenGlRender(GlInterface gl, int fb)
        {
            gl.ClearColor(0.9f, 0.9f, 0.9f, 1f);
            gl.Clear(GL_COLOR_BUFFER_BIT | GL_DEPTH_BUFFER_BIT);
            gl.Enable(GL_DEPTH_TEST);
            gl.Viewport(0, 0, (int)Bounds.Width, (int)Bounds.Height);
            var GL = gl;

            GL.BindBuffer(GL_ARRAY_BUFFER, _vertexBufferObject);
            GL.BindBuffer(GL_ELEMENT_ARRAY_BUFFER, _facesBufferObject);
            _glExt.BindVertexArray(_vertexArrayObject);
            GL.UseProgram(_shaderProgram);
            CheckError(GL);
            var projection =
                System.Numerics.Matrix4x4.CreatePerspectiveFieldOfView((float)(Math.PI / 4), (float)(Bounds.Width / Bounds.Height),
                    0.01f, 1000);


            var view = System.Numerics.Matrix4x4.CreateLookAt(CameraPosition, new Vector3(), new Vector3(0, -1, 0));
            var model = System.Numerics.Matrix4x4.CreateFromYawPitchRoll(_yaw, _pitch, _roll);
            var modelLoc = GL.GetUniformLocationString(_shaderProgram, "uModel");
            var viewLoc = GL.GetUniformLocationString(_shaderProgram, "uView");
            var projectionLoc = GL.GetUniformLocationString(_shaderProgram, "uProjection");
            var maxYLoc = GL.GetUniformLocationString(_shaderProgram, "uMaxY");
            var minYLoc = GL.GetUniformLocationString(_shaderProgram, "uMinY");
            var timeLoc = GL.GetUniformLocationString(_shaderProgram, "uTime");
            var discoLoc = GL.GetUniformLocationString(_shaderProgram, "uDisco");
            GL.UniformMatrix4fv(modelLoc, 1, false, &model);    
            GL.UniformMatrix4fv(viewLoc, 1, false, &view);
            GL.UniformMatrix4fv(projectionLoc, 1, false, &projection);
            GL.Uniform1f(maxYLoc, _maxY);
            GL.Uniform1f(minYLoc, _minY);
            GL.Uniform1f(timeLoc, (float)St.Elapsed.TotalSeconds);
            GL.Uniform1f(discoLoc, 0);
            CheckError(GL);
            GL.DrawElements(GL_TRIANGLES, _faces.Count * 3, GL_UNSIGNED_SHORT, IntPtr.Zero);

            CheckError(GL);
        }

        unsafe void RecursiveLoadScene(Node node, Scene scene)
        {
            for (int m = 0; m < node.MeshCount; m++)
            {
                Mesh mesh = scene.Meshes[node.MeshIndices[m]];
                for(int i = 0; i < mesh.Vertices.Count; i++)
                {
                    var vertice = mesh.Vertices[i];

                    Vertex vertex = new Vertex();
                    
                    vertex.Position = new Vector3(vertice.X, vertice.Y, vertice.Z);

                    if (mesh.HasNormals)
                    {
                        var normal = mesh.Normals[i];
                        vertex.Normal = new Vector3(normal.X, normal.Y, normal.Z);
                    }

                    _points.Add(vertex);
                    _maxY = Math.Max(_maxY, vertice.Y);
                    _minY = Math.Min(_minY, vertice.Y);
                }

                foreach (Face face in mesh.Faces)
                {
                    Triangle triangle = new Triangle();
                    triangle.X0 = (ushort)face.Indices[0];
                    triangle.X1 = (ushort)face.Indices[1];
                    triangle.X2 = (ushort)face.Indices[2];

                    _faces.Add(triangle);
                }

                //_indices.AddRange(mesh.GetIndices());
            }
            
            foreach (var child in node.Children)
            {
                RecursiveLoadScene(child, scene);
            }
        }

        private void CheckError(GlInterface gl)
        {
            int err;
            while ((err = gl.GetError()) != GL_NO_ERROR)
                Console.WriteLine(err);
        }

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        private struct Vertex
        {
            public Vector3 Position;
            public Vector3 Normal;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct Triangle
        {
            public ushort X0;
            public ushort X1;
            public ushort X2;
        }

        private GlExtrasInterface _glExt;
        private List<Vertex> _points = new List<Vertex>();
        //private List<int> _indices = new List<int>();
        private List<Triangle> _faces = new List<Triangle>();
        List<int> mBufferIds = new List<int>();
        private int _vertexArrayObject;
        private int _vertexShader;
        private int _fragmentShader;
        private int _shaderProgram;
        private int _vertexBufferObject;
        //private int _indexBufferObject;
        private int _facesBufferObject;
        
        Scene mScene;
        private float _minY;
        private float _maxY;
        static Stopwatch St = Stopwatch.StartNew();
    }

            class GlExtrasInterface : GlInterfaceBase<GlInterface.GlContextInfo>
        {
            public GlExtrasInterface(GlInterface gl) : base(gl.GetProcAddress, gl.ContextInfo)
            {
            }
            
            public delegate void GlDeleteVertexArrays(int count, int[] buffers);
            [GlMinVersionEntryPoint("glDeleteVertexArrays", 3,0)]
            [GlExtensionEntryPoint("glDeleteVertexArraysOES", "GL_OES_vertex_array_object")]
            public GlDeleteVertexArrays DeleteVertexArrays { get; }
            
            public delegate void GlBindVertexArray(int array);
            [GlMinVersionEntryPoint("glBindVertexArray", 3,0)]
            [GlExtensionEntryPoint("glBindVertexArrayOES", "GL_OES_vertex_array_object")]
            public GlBindVertexArray BindVertexArray { get; }
            public delegate void GlGenVertexArrays(int n, int[] rv);
        
            [GlMinVersionEntryPoint("glGenVertexArrays",3,0)]
            [GlExtensionEntryPoint("glGenVertexArraysOES", "GL_OES_vertex_array_object")]
            public GlGenVertexArrays GenVertexArrays { get; }
            
            public int GenVertexArray()
            {
                var rv = new int[1];
                GenVertexArrays(1, rv);
                return rv[0];
            }
        }

                static class MethodExtensions
        {
            public static double Map(this double value, double fromSource, double toSource, double fromTarget, double toTarget)
            {
                return (value - fromSource) / (toSource - fromSource) * (toTarget - fromTarget) + fromTarget;
            }

            public static float Map2(this float value, float fromSource, float toSource, float fromTarget, float toTarget)
            {
                return (value - fromSource) / (toSource - fromSource) * (toTarget - fromTarget) + fromTarget;
            }
        }
}
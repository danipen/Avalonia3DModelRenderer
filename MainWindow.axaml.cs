using System;
using System.Collections.Generic;
using Assimp;
using System.Text;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.OpenGL;
using static Avalonia.OpenGL.GlConsts;

namespace Avalonia3DModelRenderer
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            //mBrowseButton = this.FindControl<Button>("mBrowseButton");
            mBrowseButton.Click += BrowseButton_Click;
            //mContentTextBox = this.Find<TextBox>("mContentTextBox");

            mOpenGlControl = new OpenGlControl();

            mOpenGlControlContainer.Child = mOpenGlControl;
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

                mOpenGlControl.SetScene(rawScene);

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
        public void SetScene(Scene scene)
        {
            mScene = scene;
        }

        protected override void OnOpenGlInit(GlInterface gl, int fb)
        {
            base.OnOpenGlInit(gl, fb);
        }

        protected override void OnOpenGlDeinit(GlInterface gl, int fb)
        {
            base.OnOpenGlDeinit(gl, fb);
        }

        protected override void OnOpenGlRender(GlInterface gl, int fb)
        {
            gl.ClearColor(0.9f, 0.9f, 0.9f, 1f);
            gl.Clear(GL_COLOR_BUFFER_BIT | GL_DEPTH_BUFFER_BIT);
            gl.Enable(GL_DEPTH_TEST);
            gl.Viewport(0, 0, (int)Bounds.Width, (int)Bounds.Height);

            if (mScene == null)
                return;

            //RecursiveRender(gl, mScene.RootNode, mScene.Meshes);
        }

        unsafe void RecursiveRender(GlInterface gl, Node node, List<Mesh> meshes)
        {
            foreach (var mesh in meshes)
            {
                int glBufferId = gl.GenBuffer();
                gl.BindBuffer(GL_ARRAY_BUFFER, glBufferId);

                var byteCount = mesh.Vertices.Count * 12;
                var temp = new float[byteCount];

                var n = 0;
                foreach(var v in mesh.Vertices)
                {
                    temp[n++] = v.X;
                    temp[n++] = v.Y;
                    temp[n++] = v.Z;
                }   

                fixed (void* pdata = temp)
                {
                    gl.BufferData(GL_ARRAY_BUFFER, (IntPtr)(byteCount), new IntPtr(pdata), GL_STATIC_DRAW);
                }

                gl.DrawElements(GL_TRIANGLES, 3, GL_UNSIGNED_SHORT, IntPtr.Zero);
                gl.BindBuffer(GL_ARRAY_BUFFER, 0);
            }

            foreach (var child in node.Children)
            {
                RecursiveRender(gl, child, meshes);
            }
        }

        Scene mScene;
    }
}
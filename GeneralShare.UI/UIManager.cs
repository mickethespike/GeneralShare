﻿using GeneralShare.Collections;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;
using MonoGame.Extended.TextureAtlases;
using System;
using System.Collections.Generic;

namespace GeneralShare.UI
{
    public class UIManager : IDisposable
    {
        public delegate void ElementListSortedDelegate();

        public event ElementListSortedDelegate ElementListSorted;

        private static TextureRegion2D _grayscaleRegion;
        private UIElement __selectedElement;
        internal Viewport _lastViewport;

        public GraphicsDevice GraphicsDevice { get; }
        public TextureRegion2D GrayscaleRegion { get; set; }
        public TextureRegion2D WhitePixelRegion { get; set; }

        public bool Disposed { get; private set; }
        public object SyncRoot { get; private set; }
        public SamplingMode PreferredSamplingMode { get; set; }
        public Viewport Viewport => _lastViewport;
        public bool ElementsNeedSorting { get; private set; }
        public ListArray<UITransform> Transforms { get; }

        public UIElement SelectedElement
        {
            get
            {
                lock (SyncRoot)
                    return __selectedElement;
            }
            set
            {
                lock (SyncRoot)
                {
                    if (__selectedElement != null)
                        __selectedElement.IsSelected = false;
                    __selectedElement = value;
                }
            }
        }

        /// <summary>
        /// Constructs a <see cref="UIManager"/> instance using an existing grayscale region.
        /// </summary>
        /// <param name="grayscaleRegion">
        /// A <see cref="TextureRegion2D"/> containing a 1x1 white pixel
        /// at (X:0,Y:0) and a 1x1 gray pixel at (X:0,Y:1).
        /// </param>
        public UIManager(GraphicsDevice device, TextureRegion2D grayscaleRegion)
        {
            GrayscaleRegion = grayscaleRegion ?? throw new ArgumentNullException(nameof(grayscaleRegion));
            if (GrayscaleRegion.Width != 1) throw new ArgumentException();
            if (GrayscaleRegion.Height != 2) throw new ArgumentException();

            WhitePixelRegion = new TextureRegion2D(grayscaleRegion.Texture, 0, 0, 1, 1);
            GraphicsDevice = device;

            SyncRoot = new object();
            Transforms = new ListArray<UITransform>();
            Transforms.Changed += Elements_Changed;

            Input.TextInput += Input_TextInput;
        }

        /// <summary>
        /// Constructs a <see cref="UIManager"/> instance with a static grayscale texture.
        /// </summary>
        /// <param name="device">
        /// The <see cref="GraphicsDevice"/> to use for creating a
        /// grayscale texture if the existing static texture is null.
        /// </param>
        public UIManager(GraphicsDevice device) : this(device, GetRegion(device))
        {
        }

        private static TextureRegion2D GetRegion(GraphicsDevice device)
        {
            if (_grayscaleRegion == null)
            {
                var tex = new Texture2D(device, 1, 2);
                tex.SetData(new Color[] { Color.White, Color.Gray });
                _grayscaleRegion = new TextureRegion2D(tex, 0, 0, 1, 2);
            }

            return _grayscaleRegion;
        }

        private void Elements_Changed(int oldVersion, int newVersion)
        {
            FlagForSort();
        }

        private void Input_TextInput(TextInputEventArgs e)
        {
            if (Disposed == false)
            {
                lock (SyncRoot)
                {
                    if (SelectedElement != null)
                        SelectedElement.TriggerOnTextInput(e);
                }
            }
        }

        private void FlagForSort()
        {
            if (Disposed == false)
                ElementsNeedSorting = true;
        }

        public void SortElements()
        {
            lock (SyncRoot)
            {
                if (ElementsNeedSorting)
                {
                    Transforms.Sort(new UIDepthComparer());
                    ElementListSorted?.Invoke();
                    ElementsNeedSorting = false;
                }
            }
        }

        public bool GetElement(float x, float y, out UIElement output)
        {
            lock (SyncRoot)
            {
                for (int i = 0, length = Transforms.Count; i < length; i++)
                {
                    UITransform item = Transforms[i];
                    if (item is UIElement element)
                    {
                        if (element.Boundaries.Contains(new Point2(x, y)))
                        {
                            output = element;
                            return true;
                        }
                    }
                }
            }

            output = null;
            return false;
        }

        public bool GetElement(Vector2 position, out UIElement output)
        {
            return GetElement(position.X, position.Y, out output);
        }

        public bool GetElement(Point point, out UIElement output)
        {
            return GetElement(point.X, point.Y, out output);
        }

        public UIElement GetElement(float x, float y)
        {
            GetElement(x, y, out var element);
            return element;
        }

        public UIElement GetElement(Vector2 position)
        {
            return GetElement(position.X, position.Y);
        }

        private void Transform_MarkedDirty(DirtMarkType type)
        {
            if (type.HasFlagF(DirtMarkType.Position))
                FlagForSort();
        }

        public void AddElement(UITransform transform)
        {
            lock (SyncRoot)
            {
                Transforms.Add(transform);
                transform.MarkedDirty += Transform_MarkedDirty;
                FlagForSort();
            }
        }

        public bool RemoveElement(UITransform transform)
        {
            lock (SyncRoot)
            {
                int index = Transforms.FindIndex((x) => x == transform);
                if (index == -1)
                {
                    return false;
                }
                else
                {
                    Transforms[index].MarkedDirty -= Transform_MarkedDirty;
                    Transforms.RemoveAt(index);
                    
                    return true;
                }
            }
        }

        public ListArray<UITransform> GetSortedTransforms()
        {
            if (Disposed)
                throw new ObjectDisposedException(nameof(UIManager));

            lock (SyncRoot)
            {
                SortElements();
                return Transforms;
            }
        }

        public IEnumerable<UIElement> GetInputSensitiveElements()
        {
            lock (SyncRoot)
            {
                ListArray<UITransform> transforms = GetSortedTransforms();
                for (int i = 0, length = transforms.Count; i < length; i++)
                {
                    UITransform transform = transforms[i];
                    if (transform.Enabled == false)
                        continue;

                    if(transform is UIElement element)
                    if (IsElementInputSensitive(element))
                        yield return element;
                }
            }
        }

        public bool IsElementInputSensitive(UIElement element)
        {
            return element.BlockCursor || element.TriggerMouseEvents;
        }

        public void Update(GameTime time)
        {
            Viewport freshViewport = GraphicsDevice.Viewport;
            bool viewportChanged = Viewport.EqualsTo(freshViewport);
            _lastViewport = freshViewport;

            bool anyDown = Input.IsAnyMouseDown(out var down);
            bool anyPressed = Input.IsAnyMousePressed(out var pressed);
            bool anyReleased = Input.IsAnyMouseReleased(out var released);

            MouseState state = Input.NewMouseState;
            Point mousePos = state.Position;

            lock (SyncRoot)
            {
                ListArray<UITransform> transforms = GetSortedTransforms();
                for (int i = 0, count = transforms.Count; i < count; i++)
                {
                    var transform = transforms[i];
                    if (transform.Enabled == false)
                        continue;

                    if (viewportChanged)
                        transform.OnViewportChange(freshViewport);

                    transform.Update(time);

                    if (transform is UIElement element)
                    {
                        if (IsElementInputSensitive(element) == false)
                            continue;
                        lock (element.SyncRoot)
                        {
                            element.IsMouseHovering = element.Boundaries.Contains(mousePos);
                            if (element.IsMouseHovering)
                            {
                                if (element.TriggerMouseEvents)
                                {
                                    if (anyDown)
                                        element.TriggerOnMouseDown(state, pressed);

                                    if (anyPressed)
                                        element.TriggerOnMousePress(state, pressed);

                                    if (anyReleased)
                                        element.TriggerOnMouseRelease(state, released);
                                }

                                if (element.AllowSelection)
                                {
                                    if (Input.IsAnyMouseDown(out var temp))
                                        SelectedElement = element;
                                }

                                if (element.BlockCursor == true)
                                    break;
                            }
                        }
                    }
                }

                var selectedE = SelectedElement;
                if (selectedE != null)
                {
                    lock (selectedE.SyncRoot)
                    {
                        selectedE.IsSelected = true;
                        if (selectedE.TriggerKeyEvents)
                        {
                            var keysDown = Input.KeysDown;
                            var keysPressed = Input.KeysPressed;
                            var keysReleased = Input.KeysReleased;

                            for (int d = 0; d < keysDown.Count; d++)
                                selectedE.TriggerOnKeyDown(keysDown[d]);

                            for (int p = 0; p < keysPressed.Count; p++)
                                selectedE.TriggerOnKeyPress(keysPressed[p]);

                            for (int r = 0; r < keysReleased.Count; r++)
                                selectedE.TriggerOnKeyRelease(keysReleased[r]);
                        }
                    }
                }
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if(Disposed == false)
            {
                if (disposing)
                {
                    lock (SyncRoot)
                    {
                        for (int i = Transforms.Count; i-- > 0;)
                        {
                            Transforms[i]?.Dispose();
                        }
                    }
                }

                Input.TextInput -= Input_TextInput;

                Disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~UIManager()
        {
            Dispose(false);
        }
    }
}
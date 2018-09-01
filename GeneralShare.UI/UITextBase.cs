﻿using GeneralShare.Collections;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended;
using MonoGame.Extended.BitmapFonts;
using MonoGame.Extended.TextureAtlases;
using System;
using System.Text;
using Glyph = MonoGame.Extended.BitmapFonts.BitmapFont.Glyph;

namespace GeneralShare.UI
{
    public abstract class UITextBase : UIElement
    {
        protected BitmapFont _font;
        protected ListArray<Glyph> _textGlyphs;
        protected string _value;
        protected StringBuilder _processedText;
        protected ListArray<LineInfo> _lines;
        protected bool _buildQuadTree;
        protected PooledQuadTree<float> _textQuadTree;

        protected ListArray<GlyphBatchedSprite> _textSprites;
        protected ListArray<Color> _expressionColors;
        protected UIAnchor _anchor;

        protected bool _valueExpressions;
        protected bool _keepExpressions;
        protected Color _color;
        protected SizeF _measure;
        protected Rectangle? _clipRect;
        protected RectangleF _textBounds;
        protected RectangleF _totalBoundaries;
        private TextAlignment _align;

        protected bool _useShadow;
        protected BatchedSprite _shadowSprite;
        protected TextureRegion2D _shadowTex;
        protected RectangleF _shadowOffset;
        protected Point _shadowTexSrc;
        protected Color _shadowColor;
        protected bool _shadowAvailable;

        public Color BaseColor { get => _color; set => SetColor(value); }
        public BitmapFont Font { get => _font; set => SetFont(value); }
        public Rectangle? ClippingRectangle { get => _clipRect; set => SetClipRect(value); }
        public StringBuilder ProcessedText { get { ProcessTextIfNeeded(); return _processedText; } }
        public int SpecialProcessedTextLength { get; private set; }

        public TextureRegion2D ShadowTexture { get => _shadowTex; set => SetShadowTex(value); }
        public Color ShadowColor { get => _shadowColor; set => SetShadowColor(value); }
        public bool UseShadow { get => _useShadow; set => SetUseShadow(value); }
        public RectangleF ShadowOffset { get => _shadowOffset; set => SetShadowOffset(value); }

        public bool BuildTextQuadTree { get => _buildQuadTree; set => SetBuildTextTree(value); }
        public ReadOnlyQuadTree<float> CharQuadTree => _textQuadTree.CurrentTree.AsReadOnly();
        public override RectangleF Boundaries { get { ProcessTextIfNeeded(); return _totalBoundaries; } }
        public RectangleF TextBoundaries { get { ProcessTextIfNeeded(); return _textBounds; } }
        public SizeF Measure { get { ProcessTextIfNeeded(); return _measure; } }
        public TextAlignment Alignment { get => _align; set => SetAlign(value); }

        public UIAnchor Anchor => GetAnchor();
        public PivotPosition Pivot { get => GetPivot(); set => Anchor.Pivot = value; }
        public Vector3 PivotOffset { get => GetPivotOffset(); set => Anchor.PivotOffset = value; }

        public UITextBase(UIManager manager, BitmapFont font) : base(manager)
        {
            _value = string.Empty;
            _textGlyphs = new ListArray<Glyph>();
            _processedText = new StringBuilder();
            _textQuadTree = new PooledQuadTree<float>(Rectangle.Empty, 2, true);
            _lines = new ListArray<LineInfo>();

            _textSprites = new ListArray<GlyphBatchedSprite>();
            _expressionColors = new ListArray<Color>();
            
            Font = font;
            BaseColor = Color.White;
            Scale = Vector2.One;

            SetShadowRadius(5, 0);
            ShadowColor = new Color(Color.Gray, 0.5f);
            if (manager != null)
                ShadowTexture = manager.WhitePixelRegion;
        }

        public UITextBase(BitmapFont font) : this(null, font) { }

        private UIAnchor GetAnchor()
        {
            if (_anchor == null)
                _anchor = new UIAnchor(Manager);
            return _anchor;
        }

        private PivotPosition GetPivot()
        {
            if (_anchor == null)
                return PivotPosition.None;
            return _anchor.Pivot;
        }

        private Vector3 GetPivotOffset()
        {
            if (_anchor == null)
                return Vector3.Zero;
            return Anchor.PivotOffset;
        }

        protected void SetAlign(TextAlignment align)
        {
            MarkDirty(ref _align, align, DirtMarkType.TextAlignment);
        }

        protected void SetBuildTextTree(bool value)
        {
            MarkDirtyE(ref _buildQuadTree, value, DirtMarkType.BuildQuadTree);
        }

        public void SetShadowRadius(float radius)
        {
            SetShadowOffset(new RectangleF(-radius, -radius, radius * 2, radius * 2));
        }

        public void SetShadowRadius(float width, float height)
        {
            SetShadowOffset(new RectangleF(-width, -height, width * 2, height * 2));
        }

        protected void SetShadowOffset(RectangleF value)
        {
            value.X -= 1;
            value.Width += 1;
            MarkDirtyE(ref _shadowOffset, value, DirtMarkType.ShadowOffset);
        }

        protected void SetShadowColor(Color value)
        {
            MarkDirtyE(ref _shadowColor, value, DirtMarkType.ShadowColor);
        }

        protected void SetShadowTex(TextureRegion2D value)
        {
            _shadowTex = value;
            _shadowTexSrc = value.Bounds.Size;
        }

        protected void SetUseShadow(bool value)
        {
            MarkDirty(ref _useShadow, value, DirtMarkType.UseShadow);
        }

        protected void SetFont(BitmapFont value)
        {
            MarkDirty(ref _font, value, DirtMarkType.Font);
        }

        protected void SetValueExp(bool value)
        {
            MarkDirtyE(ref _valueExpressions, value, DirtMarkType.UseTextExpressions);
        }

        protected void SetKeepExp(bool value)
        {
            MarkDirtyE(ref _keepExpressions, value, DirtMarkType.KeepTextExpressions);
        }

        protected void SetValue(string value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            MarkDirty(ref _value, value, DirtMarkType.Value);
        }

        protected void SetColor(Color value)
        {
            MarkDirtyE(ref _color, value, DirtMarkType.Color);
        }

        protected void SetClipRect(Rectangle? value)
        {
            MarkDirty(ref _clipRect, value, DirtMarkType.ClipRectangle);
        }

        public void BuildGraphicsIfNeeded()
        {
            if (Dirty == true)
            {
                BuildGraphics();
                Dirty = false;
            }
        }

        private void BuildGraphics()
        {
            ProcessTextIfNeeded();

            lock (SyncRoot)
            {
                bool hadDirtyGraphics = HasDirtMarks(
                    DirtMarkType.ValueProcessed | DirtMarkType.Transform |
                    DirtMarkType.Font | DirtMarkType.ClipRectangle);

                if (hadDirtyGraphics)
                {
                    Vector3 pos = Position;
                    _textBounds.X = pos.X;
                    _textBounds.Y = pos.Y;

                    if (_processedText.Length > 0 && _font != null)
                    {
                        _textSprites.Clear(false);
                        BitmapFontExtensions.GetGlyphSprites(
                            _textGlyphs, _textSprites, pos.ToVector2(), _color, Rotation, Origin, Scale, pos.Z, _clipRect);

                        if (_buildQuadTree)
                            PrepareQuadTree();

                        int expressionCount = _expressionColors.Count;
                        int itemCount = _textSprites.Count;
                        for (int i = 0; i < itemCount; i++)
                        {
                            if (_valueExpressions && i < expressionCount)
                            {
                                ref var sprite = ref _textSprites.GetReferenceAt(i);
                                if (_buildQuadTree)
                                    AddTextQuadItem(ref sprite);

                                Color color = _expressionColors.GetReferenceAt(i);
                                sprite.SetColor(color);
                            }
                        }

                        MarkDirty(DirtMarkType.ShadowMath, true);
                    }
                }

                if (HasDirtMarks(DirtMarkType.BuildQuadTree))
                {
                    if (_buildQuadTree)
                    {
                        if (!hadDirtyGraphics)
                            BuildQuadTree();
                    }
                    else
                        _textQuadTree.ClearPool();

                    ClearDirtMarks(DirtMarkType.BuildQuadTree);
                }

                if (HasDirtMarks(DirtMarkType.ShadowColor))
                    _shadowSprite.SetColor(_shadowColor);

                UpdateTotalBoundaries();
                SpecialBoundaryUpdate(_totalBoundaries, out _totalBoundaries);

                if (HasDirtMarks(DirtMarkType.ShadowMath))
                    UpdateShadowSprite();
                _shadowAvailable = _useShadow && _shadowTex != null;

                ClearDirtMarks();
                InvokeMarkedDirty(DirtMarkType.Boundaries);
            }
        }

        private void GetLines()
        {
            if (_align != TextAlignment.Center && _align != TextAlignment.Right)
                return;

            float width = 0;

            void AddLine(int index)
            {
                float scaledWidth = width * Scale.X;
                _lines.Add(new LineInfo(
                    scaledWidth,
                    index - _lines.Count // pretend that newline chars don't exist
                ));
                width = 0;
            }

            for (int i = 0; i < _textGlyphs.Count; i++)
            {
                ref Glyph glyph = ref _textGlyphs.GetReferenceAt(i);
                if (glyph.FontRegion != null)
                {
                    float right = glyph.Position.X + glyph.FontRegion.Width;
                    if (right > width)
                        width = right;
                }

                if (glyph.Character == '\n')
                {
                    width = 0;
                    AddLine(i);
                }
            }
            AddLine(_textGlyphs.Count - 1);

            float GetXOffset(float lineWidth)
            {
                switch (_align)
                {
                    case TextAlignment.Center:
                        return -lineWidth / 2;

                    case TextAlignment.Right:
                        return -lineWidth;

                    default:
                        return 0;
                }
            }

            int lastBreak = 0;
            int lineCount = _lines.Count;
            for (int i = 0; i < lineCount; i++)
            {
                int breakIndex = _lines[i].BreakIndex;
                int charCount = i == lineCount - 1 ? breakIndex + 1 : breakIndex;

                for (int j = lastBreak; j < charCount; j++)
                {
                    float xOffset = GetXOffset(_lines[i].Width);

                    ref BatchedSprite sprite = ref _textSprites.GetReferenceAt(j).Sprite;
                    sprite.TL.Position.X += xOffset;
                    sprite.TR.Position.X += xOffset;
                    sprite.BL.Position.X += xOffset;
                    sprite.BR.Position.X += xOffset;
                }
                lastBreak = breakIndex;
            }
            _lines.Clear(false);
        }

        public void ProcessTextIfNeeded()
        {
            const DirtMarkType needsProcessingFlags =
                DirtMarkType.Color | DirtMarkType.Value | DirtMarkType.UseTextExpressions |
                DirtMarkType.Font | DirtMarkType.InputPrefix | DirtMarkType.KeepTextExpressions;

            lock (SyncRoot)
            {
                bool neededProcessing = HasDirtMarks(needsProcessingFlags);
                if (neededProcessing)
                {
                    ClearDirtMarks(needsProcessingFlags);

                    _expressionColors.Clear(false);
                    _processedText.Clear();

                    SpecialBeforeTextProcessing();
                    SpecialProcessedTextLength = _processedText.Length;

                    if (string.IsNullOrEmpty(_value) == false)
                    {
                        ListArray<Color> colorOutput = _valueExpressions ? _expressionColors : null;
                        SpecialTextFormat.Format(_value, _processedText, _color, _font, _keepExpressions, colorOutput);
                    }

                    SpecialPostTextProcessing();
                    
                    _textBounds.Position = Position.ToVector2();
                    _textGlyphs.Clear(false);
                    _textBounds.Size = _font.GetGlyphs(_processedText, _textGlyphs) * Scale;
                    UpdateTotalBoundaries();
                    InvokeMarkedDirty(DirtMarkType.Boundaries);

                    GetLines();
                    MarkDirty(DirtMarkType.ValueProcessed, true);
                }
            }
        }

        protected virtual void SpecialBeforeTextProcessing()
        {
        }

        protected virtual void SpecialPostTextProcessing()
        {
        }

        protected virtual void SpecialBoundaryUpdate(RectangleF input, out RectangleF output)
        {
            output = input;
        }

        protected void PrepareQuadTree()
        {
            _textQuadTree.ClearTree();
            _textQuadTree.Resize(_textBounds);
        }

        protected void BuildQuadTree()
        {
            PrepareQuadTree();
            for (int i = 0, count = _textSprites.Count; i < count; i++)
            {
                AddTextQuadItem(ref _textSprites.GetReferenceAt(i));
            }
        }

        protected void AddTextQuadItem(ref GlyphBatchedSprite item)
        {
            float yDiff = item.Sprite.TL.Position.Y - Position.Y;
            int line = (int)Math.Floor(yDiff / Scale.Y / _font.LineHeight);

            float x = (float)Math.Floor(item.Sprite.TL.Position.X);
            float y = (float)Math.Floor(Position.Y) + line * _font.LineHeight * Scale.Y;
            float w = (float)Math.Floor(item.Sprite.BR.Position.X - x) * 0.5f;
            float h = _font.LineHeight * Scale.Y * 0.5f;

            float o = 0.001f;
            var node = new RectangleF(x + o, y + h * 0.5f, w - o, h);

            _textQuadTree.CurrentTree.Insert(node, item.Index);

            node.X += w;
            _textQuadTree.CurrentTree.Insert(node, item.Index + 0.5f);
        }

        protected void UpdateTotalBoundaries()
        {
            _totalBoundaries = _textBounds + _shadowOffset;
        }

        protected void UpdateShadowSprite()
        {
            if (_shadowTexSrc.X <= 0 || _shadowTexSrc.Y <= 0)
                return;

            Matrix2 matrix = BatchedSprite.GetMatrixFromRect(
                _totalBoundaries, Origin, Rotation, _shadowTexSrc);

            _shadowSprite.SetTransform(matrix, _shadowTexSrc);
            _shadowSprite.SetDepth(Position.Z);
            _shadowSprite.SetTexCoords(_shadowTex);
        }

        public override void Draw(GameTime time, SpriteBatch batch)
        {
            lock (SyncRoot)
            {
                if (_anchor != null)
                {
                    if (_anchor.Pivot != PivotPosition.None)
                        Position = _anchor.Position;
                }

                BuildGraphicsIfNeeded();

                if (_textSprites.Count > 0)
                {
                    if (_shadowAvailable)
                        batch.Draw(_shadowTex.Texture, _shadowSprite);

                    batch.DrawString(_textSprites);
                }
            }
        }

        protected struct LineInfo
        {
            public float Width;
            public int BreakIndex;

            public LineInfo(float width, int breakIndex)
            {
                Width = width;
                BreakIndex = breakIndex;
            }
        }
    }
}

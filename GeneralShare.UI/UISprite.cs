﻿using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended;
using MonoGame.Extended.TextureAtlases;

namespace GeneralShare.UI
{
    public class UISprite : UIElement
    {
        private TextureRegion2D _region;
        private BatchedSprite _sprite;
        private RectangleF _boundaries;
        private RectangleF _destination;

        public override RectangleF Boundaries => _boundaries;

        public TextureRegion2D Region { get => _region; set => SetTexture(value); }
        public Color Color { get => _sprite.TL.Color; set => _sprite.SetColor(value); }
        public RectangleF Destination { get => _destination; set => SetDestination(value); }
        public bool IsUsingDestination => Destination.Width > 0 && Destination.Height > 0;

        public UISprite(UIManager manager, TextureRegion2D texture) : base(manager)
        {
            Region = texture;
            Color = Color.White;
        }

        private void SetTexture(TextureRegion2D value)
        {
            MarkDirty(ref _region, value, DirtMarkType.Value);
        }

        private void SetDestination(RectangleF value)
        {
            MarkDirty(ref _destination, value, DirtMarkType.Destination);
        }

        private void UpdateSprite()
        {
            if (DirtMarks.HasAnyFlag(DirtMarkType.Transform, DirtMarkType.Value, DirtMarkType.Destination))
            {
                ClearDirtMarks();
                var srcSize = _region.Bounds.Size.ToVector2();
                if (IsUsingDestination)
                {
                    var matrix = BatchedSprite.GetMatrixFromRect(_destination, Origin, -Rotation, srcSize);
                    _sprite.SetTransform(matrix, srcSize);
                }
                else
                {
                    _boundaries = new RectangleF(X, Y, Region.Width * Scale.X, Region.Height * Scale.Y);

                    var pos = GlobalPosition.ToVector2();
                    _sprite.SetTransform(pos, -Rotation, Scale, Origin * srcSize, srcSize);
                }
                _sprite.SetDepth(Z);
                _sprite.SetTexCoords(_region);

                InvokeMarkedDirty(DirtMarkType.Boundaries);
            }
        }

        public override void Draw(GameTime time, SpriteBatch batch)
        {
            lock (SyncRoot)
            {
                if (Dirty)
                {
                    UpdateSprite();
                    Dirty = false;
                }
                batch.Draw(_region.Texture, _sprite);
            }
        }
    }
}

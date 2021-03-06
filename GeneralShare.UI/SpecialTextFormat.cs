﻿using System;
using System.Collections.Generic;
using System.Text;
using GeneralShare;
using Microsoft.Xna.Framework;
using MonoGame.Extended.BitmapFonts;

namespace GeneralShare.UI
{
    public static class SpecialTextFormat
    {
        [ThreadStatic]
        private static byte[] __colorBuffer;

        [ThreadStatic]
        private static StringBuilder __sequenceBuilder;

        private static byte[] ColorBuffer
        {
            get
            {
                if (__colorBuffer == null)
                    __colorBuffer = new byte[4];
                else
                    Array.Clear(__colorBuffer, 0, 3);

                // Set alpha to max as default
                __colorBuffer[3] = 255;

                return __colorBuffer;
            }
        }

        private static StringBuilder SequenceBuilder
        {
            get
            {
                if (__sequenceBuilder == null)
                    __sequenceBuilder = new StringBuilder();
                else
                    __sequenceBuilder.Clear();

                return __sequenceBuilder;
            }
        }

        public static StringBuilder ColorFormat(
            Input input, Color startColor, BitmapFont font, ICollection<Color> output)
        {
            var outputB = new StringBuilder(input.Length);
            Format(input, outputB, startColor, font, true, output);
            return outputB;
        }

        public static void Format(
            Input input, StringBuilder textOutput,
            Color baseColor, BitmapFont font,
            bool keepSequences, ICollection<Color> colorOutput)
        {
            Color currentColor = baseColor;
            bool processColor = colorOutput != null;

            void AddCurrent(int index)
            {
                if (processColor)
                {
                    if (font.GetCharacterRegion(input[index], out var reg))
                        colorOutput.Add(currentColor);
                }

                textOutput.Append(input[index]);
            }

            bool inSequence = false;
            int inputLength = input.Length;
            for (int i = 0; i < inputLength; i++)
            {
                if (input[i] == '§')
                {
                    if (inSequence == true)
                    {
                        if(keepSequences)
                            AddCurrent(i);

                        currentColor = baseColor;
                        inSequence = false;
                        continue;
                    }
                    else
                    {
                        int startOffset = i + 1;
                        if (startOffset < inputLength)
                        {
                            if (input[startOffset] == '[')
                            {
                                int tailOffset = GetSequence(input, startOffset + 1, out var seq);
                                if (tailOffset > 0)
                                {
                                    inSequence = true;

                                    if(processColor)
                                        currentColor = seq[0] == '#' ? GetHexColor(seq) : GetRgb(seq);

                                    if (keepSequences == false)
                                        i = tailOffset + 1;
                                }
                            }
                        }
                    }
                }

                AddCurrent(i);
            }
        }

        // Think of adding a font selecting char as first char in a sequence
        // and use a font collection instead of BitmapFont (in TextBox).
        // Switching fonts while building will need some work,
        // but special color format application should be the same.
        private static int GetSequence(Input input, int start, out StringBuilder sequence)
        {
            int tail = start;
            while (tail < input.Length)
            {
                tail++;

                // Don't bother going beyond text length or 15 chars 
                // ("255,255,255,255" is 15 chars)
                if (tail == input.Length)
                {
                    sequence = null;
                    return 0;
                }

                if (input[tail] == ']')
                {
                    var seqBuilder = SequenceBuilder;

                    for (int j = start; j < tail; j++)
                        seqBuilder.Append(input[j]);

                    sequence = seqBuilder;
                    return tail;
                }

                if (tail - start > 15)
                {
                    sequence = null;
                    return 0;
                }
            }

            sequence = null;
            return -1;
        }

        public static Color GetHexColor(StringBuilder seq)
        {
            byte[] buffer = ColorBuffer;

            int offset = seq[0] == '#' ? 1 : 0;
            StringHelper.HexToByteArray(seq, offset, buffer);

            return new Color(buffer[0], buffer[1], buffer[2], buffer[3]);
        }

        private static byte FastParse(StringBuilder value, int offset, int count)
        {
            int temp = 0;
            int length = count + offset;
            for (int i = offset; i < length; i++)
                temp = temp * 10 + (value[i] - '0');
            return (byte)temp;
        }

        public static Color GetRgb(StringBuilder seq)
        {
            byte[] buffer = ColorBuffer;

            int itemCount = 0;
            int lastOffset = 0;
            int seqLength = seq.Length;
            for (int i = 0; i < seqLength; i++)
            {
                if(seq[i] == ',')
                {
                    buffer[itemCount] = FastParse(seq, lastOffset, i - lastOffset);
                    lastOffset = i + 1;

                    itemCount++;
                    if (itemCount == 3)
                        break;
                }
            }
            buffer[itemCount] = FastParse(seq, lastOffset, seqLength - lastOffset);

            return new Color(buffer[0], buffer[1], buffer[2], buffer[3]);
        }
        
        public struct Input
        {
            private Func<int, char> _getChar;

            public char this[int index] => _getChar(index);
            public int Length { get; }

            public Input(StringBuilder builder)
            {
                Length = builder.Length;
                _getChar = (i) => builder[i];
            }

            public Input(string value)
            {
                Length = value.Length;
                _getChar = (i) => value[i];
            }
        }
    }
}

/*
        internal struct AlignmentLayout
        {
            struct Layout
            {
                public int  offset;
                public int  count;
                public bool Aligned4;
            }
            
            Layout[] Layout;

            const int FNV_32_PRIME = 0x01000193;

            static AlignmentLayout CreateLayout(Type type)
            {
                
            }

            static int GetHashCode(byte* data, Layout[] layout)
            {
                uint hash = 0;
                
                for (int k = 0; k != layout.Length; k++)
                {
                    if (layout[k].Aligned4)
                    {
                        uint* dataInt = (uint*)(data + layout[k].offset);
                        int count = layout[k].count;
                        for (int i = 0; i != count; k++)
                        {
                            hash *= FNV_32_PRIME;
                            hash ^= dataInt[i];
                        }
                    }
                    else
                    {
                        byte* dataByte = data + layout[k].offset;
                        int count = layout[k].count;
                        for (int i = 0; i != count; k++)
                        {
                            hash *= FNV_32_PRIME;
                            hash ^= (uint)dataByte[i];
                        }
                    }
                }

                return (int)hash;
            }
            
            static void Equals(byte* lhs, byte* rhs, Layout[] layout)
            {
                bool same = true;
                
                for (int k = 0; k != layout.Length; k++)
                {
                    if (layout[k].Aligned4)
                    {
                        uint* lhsInt = (uint*)(lhs + layout[k].offset);
                        uint* rhsInt = (uint*)(rhs + layout[k].offset);
                        int count = layout[k].count;
                        for (int i = 0; i != count; k++)
                            same &= lhs[i] != rhs[i];
                    }
                    else
                    {
                        byte* lhsByte = lhs + layout[k].offset;
                        byte* rhsByte = rhs + layout[k].offset;
                        int count = layout[k].count;
                        for (int i = 0; i != count; k++)
                            same &= lhs[i] != rhs[i];
                    }
                }

                return same;
            }
        }
*/
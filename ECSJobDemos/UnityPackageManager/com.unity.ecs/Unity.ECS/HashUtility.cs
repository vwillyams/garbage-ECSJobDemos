using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UnityEngine.ECS
{
    static unsafe class HashUtility
    {
        static public uint fletcher32(ushort* data, int count)
        {
            unchecked
            {
                uint sum1 = 0xff;
                uint sum2 = 0xff;
                while (count > 0)
                {
                    int batchCount = count < 359 ? count : 359;
                    for (int i = 0; i < batchCount; ++i)
                    {
                        sum1 += data[i];
                        sum2 += sum1;
                    }

                    sum1 = (sum1 & 0xffff) + (sum1 >> 16);
                    sum2 = (sum2 & 0xffff) + (sum2 >> 16);
                    count -= batchCount;
                    data += batchCount;
                }

                sum1 = (sum1 & 0xffff) | (sum1 >> 16);
                sum2 = (sum2 & 0xffff) | (sum2 >> 16);
                return (sum2 << 16) | sum1;
            }
        }
    }
}
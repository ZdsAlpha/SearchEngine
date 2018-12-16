using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Helper
{
    public static class Functions
    {
        static readonly string[] SizeSuffixes =
                   {"bytes", "KB", "MB", "GB", "TB", "PB", "EB", "ZB", "YB" };
        public static string SizeSuffix(long value, int decimalPlaces = 2)
        {
            if (decimalPlaces < 0) { throw new ArgumentOutOfRangeException("decimalPlaces"); }
            if (value < 0) { return "-" + SizeSuffix(-value); }
            if (value == 0) { return string.Format("{0:n" + decimalPlaces + "} bytes", 0); }
            int mag = (int)Math.Log(value, 1024);
            decimal adjustedSize = (decimal)value / (1L << (mag * 10));
            if (Math.Round(adjustedSize, decimalPlaces) >= 1000)
            {
                mag += 1;
                adjustedSize /= 1024;
            }
            return string.Format("{0:n" + decimalPlaces + "} {1}",
                adjustedSize,
                SizeSuffixes[mag]);
        }
        public static void QuickSort(byte[] data, int size, int offset, int length)
        {
            if (data.Length % size != 0) throw new Exception("Invalid data or size.");
            if (offset + length > size) throw new Exception("Invalid offset or length.");
            quicksort(data, size, offset, length, 0, data.Length / size - 1);
        }
        private static void quicksort(byte[] data, int size, int offset, int length, int left, int right)
        {
            Stack<Tuple<int, int>> stack = new Stack<Tuple<int, int>>();
            stack.Push(new Tuple<int, int>(left, right));
            while (stack.Count != 0)
            {
                Tuple<int, int> args = stack.Pop();
                left = args.Item1;
                right = args.Item2;
                if (left > right || left < 0 || right < 0) continue;
                int index = partition(data, size, offset, length, left, right);
                if (index != -1)
                {
                    stack.Push(new Tuple<int, int>(left, index - 1));
                    stack.Push(new Tuple<int, int>(index + 1, right));
                }
            }
        }
        private static int partition(byte[] data, int size, int offset, int length, int left, int right)
        {
            if (left > right) return -1;
            int end = left;
            byte[] pivot = new byte[length];
            Buffer.BlockCopy(data, right * size + offset, pivot, 0, length);
            for (int i = left; i < right; i++)
            {
                bool is_less = true;
                for (int j = length - 1; j >= 0; j--)
                    if (j == 0)
                        is_less = data[i * size + offset + j] < pivot[j];
                    else if (data[i * size + offset + j] < pivot[j])
                        break;
                    else if (data[i * size + offset + j] > pivot[j])
                    {
                        is_less = false;
                        break;
                    }
                if (is_less)
                {
                    swap(data, size, i, end);
                    end++;
                }
            }
            swap(data, size, end, right);
            return end;
        }
        private static void swap(byte[] data, int size, int left, int right)
        {
            byte[] temp = new byte[size];
            Buffer.BlockCopy(data, left * size, temp, 0, size);
            Buffer.BlockCopy(data, right * size, data, left * size, size);
            Buffer.BlockCopy(temp, 0, data, right * size, size);
        }
    }
}

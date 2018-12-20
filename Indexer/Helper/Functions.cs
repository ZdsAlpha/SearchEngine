using System;
using System.Collections.Generic;
using System.IO;
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
        public static bool VerifyIO(string[] inputs, string[] outputs)
        {
            foreach (var file in inputs)
                if (!File.Exists(file))
                {
                    Console.WriteLine("File " + file + " not found!");
                    return false;
                }
            if (outputs.Any((path) => File.Exists(path)))
            {
                Console.WriteLine("WARNING: Some files will be replaced.");
                Console.WriteLine("Press any key to continue...");
                Console.ReadKey();
            }
            return true;
        }
        public static void QuickSort(byte[] data, int size, int offset, int length)
        {
            QuickSort(data, size, offset, length, 0, data.Length / size - 1);
        }
        public static void QuickSort(byte[] data, int size, int offset, int length, int left, int right)
        {
            if (data.Length % size != 0) throw new Exception("Invalid data or size.");
            if (offset + length > size) throw new Exception("Invalid offset or length.");
            quicksort(data, size, offset, length, left, right);
        }
        private static void quicksort(byte[] data, int size, int offset, int length, int left, int right)
        {
            Random random = new Random();
            Stack<Tuple<int, int>> stack = new Stack<Tuple<int, int>>();
            stack.Push(new Tuple<int, int>(left, right));
            while (stack.Count != 0)
            {
                Tuple<int, int> args = stack.Pop();
                left = args.Item1;
                right = args.Item2;
                if (left > right || left < 0 || right < 0) continue;
                int index = partition(data, size, offset, length, left, right, random);
                if (index != -1)
                {
                    stack.Push(new Tuple<int, int>(left, index - 1));
                    stack.Push(new Tuple<int, int>(index + 1, right));
                }
            }
        }
        private static int partition(byte[] data, int size, int offset, int length, int left, int right, Random random)
        {
            if (left > right) return -1;
            int pivot_index = left + (int)Math.Floor((right - left + 1) * random.NextDouble());
            if (pivot_index != right) swap(data, size, pivot_index, right);
            return partition(data, size, offset, length, left, right);
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
        public static uint[] Index1(byte[] data, int size, int offset)
        {
            offset += 3;
            if (data.Length % size != 0) throw new Exception("Invalid data or size.");
            if (offset + 1 > size) throw new Exception("Invalid offset or length.");
            uint[] index = new uint[256];
            int index_id = 0;
            uint cf;
            for (cf = 0; cf < data.Length / size; cf++)
            {
                if (index_id < data[cf * size * offset])
                {
                    while (index_id != data[cf * size * offset])
                    {
                        index[index_id] = cf;
                        index_id++;
                    }
                }
            }
            for (int i = index_id; i < 256; i++)
                index[i] = cf;
            return index;
        }
        public static uint[] Index2(byte[] data, int size, int offset)
        {
            offset += 2;
            if (data.Length % size != 0) throw new Exception("Invalid data or size.");
            if (offset + 2 > size) throw new Exception("Invalid offset or length.");
            uint[] index = new uint[256 * 256];
            int index_id = 0;
            uint cf;
            for (cf = 0; cf < data.Length / size; cf++)
            {
                if (index_id / 256 < data[cf*size+offset+1] || (index_id / 256 == data[cf * size + offset + 1] && index_id % 256 < data[cf * size + offset]))
                {
                    while(!(index_id / 256 == data[cf * size + offset + 1] && index_id % 256 == data[cf * size + offset]))
                    {
                        index[index_id] = cf;
                        index_id++;
                    }
                }
            }
            for (int i = index_id; i < 256 * 256; i++)
                index[i] = cf;
            return index;
        }
        public static Tuple<int,int> BinarySearch(byte[] data, int size, int offset, byte[] obj, uint[] index)
        {
            int left;
            int right;
            BinarySearch(data, size, offset, obj, out left, out right, index);
            return new Tuple<int, int>(left, right);
        }
        public static Tuple<int,int> BinarySearch(byte[] data, int size, int offset, byte[] obj)
        {
            int left = 0;
            int right = data.Length / size - 1;
            BinarySearch(data, size, offset, obj, ref left, ref right);
            return new Tuple<int, int>(left, right);
        }
        public static void BinarySearch(byte[] data, int size, int offset, byte[] obj, out int left, out int right, uint[] index)
        {
            byte msb = obj[obj.Length - 1];
            byte lsb = obj[obj.Length - 2];
            if (msb == 0 && lsb == 0)
                left = 0;
            else
                left = (int)index[msb * 256 + lsb - 1];
            right = (int)index[msb * 256 + lsb] - 1;
            BinarySearch(data, size, offset, obj, ref left, ref right);
        }
        public static void BinarySearch(byte[] data, int size, int offset, byte[] obj, ref int left, ref int right)
        {
            int _left = left;
            int _right = right;
            if (data.Length % size != 0) throw new Exception("Invalid data or size.");
            if (offset + obj.Length > size) throw new Exception("Invalid offset or length.");
            while(left <= right)
            {
                int mid = (left + right) / 2;
                bool is_less = true;
                bool is_equal = false;
                for (int j = obj.Length - 1; j >= 0; j--)
                    if (j == 0 && data[mid * size + offset + j] == obj[j])
                    {
                        is_less = false;
                        is_equal = true;
                    }
                    else if (data[mid * size + offset + j] < obj[j])
                        break;
                    else if (data[mid * size + offset + j] > obj[j])
                    {
                        is_less = false;
                        break;
                    }
                if (is_equal)
                {
                    left = ++mid;
                    right = left - 1;
                    for (left = left - 1; left >= _left; left--)
                    {
                        is_equal = true;
                        for (int j = obj.Length - 1; j >= 0; j--)
                            if (data[left * size + offset + j] != obj[j])
                            {
                                is_equal = false;
                                break;
                            }
                        if (!is_equal) break;
                    }
                    left++;
                    for (right = right + 1; right <= _right; right++)
                    {
                        is_equal = true;
                        for (int j = obj.Length - 1; j >= 0; j--)
                            if (data[right * size + offset + j] != obj[j])
                            {
                                is_equal = false;
                                break;
                            }
                        if (!is_equal) break;
                    }
                    right--;
                    return;
                }
                else if (is_less)
                    left = mid + 1;
                else
                    right = mid - 1;
            }
            right = left - 1;
        }
    }
}

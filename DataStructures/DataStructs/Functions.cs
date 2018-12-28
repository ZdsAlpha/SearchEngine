using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace DataStructs
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
            if (right * size + offset + length > data.Length)
                throw new Exception("Right index out of range!");
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
            bool all_equal = true;
            byte[] pivot = new byte[length];
            Buffer.BlockCopy(data, right * size + offset, pivot, 0, length);
            for (int i = left; i < right; i++)
            {
                int comparison = 0;
                for (int j = length - 1; j >= 0; j--)
                    if (data[i * size + offset + j] < pivot[j])
                    {
                        comparison = -1;
                        break;
                    }
                    else if (data[i * size + offset + j] > pivot[j]) 
                    {
                        comparison = 1;
                        break;
                    }
                if (comparison > 0)
                {
                    all_equal = false;
                }
                else if (comparison < 0) 
                {
                    swap(data, size, i, end);
                    end++;
                    all_equal = false;
                }
                else
                {
                    //end++;
                }
            }
            if (all_equal) return -1;
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
        public static uint[] Index(byte[] data, int size, int offset, int depth = 2)
        {
            if (data.Length % size != 0) throw new Exception("Invalid data or size!");
            if (offset + 4 > size) throw new Exception("Invalid offset or size");
            if (depth < 1 || depth > 3) throw new Exception("Invalid depth");
            uint index_size = 1;
            for (int i = 0; i < depth; i++) index_size *= 256;
            uint[] index = new uint[index_size];
            byte[] index_bytes = new byte[4];
            uint index_id = 0;
            uint cf;
            for (cf = 0; cf < data.Length / size; cf++)
            {
                bool is_less = true;
                for (int i = 0; i < depth; i++)
                    if (i == depth - 1)
                        is_less = index_bytes[depth - i - 1] < data[cf * size + offset + 4 - i - 1];
                    else if (index_bytes[depth - i - 1] < data[cf * size + offset + 4 - i - 1])
                        break;
                    else if (index_bytes[depth - i - 1] > data[cf * size + offset + 4 - i - 1])
                    {
                        is_less = false;
                        break;
                    }
                if (is_less)
                {
                    bool is_equal;
                    do
                    {
                        index[index_id] = cf;
                        index_id++;
                        if (index_bytes[0] != byte.MaxValue)
                            index_bytes[0]++;
                        else
                        {
                            if (index_bytes[1] != byte.MaxValue)
                                index_bytes[1]++;
                            else
                            {
                                if (index_bytes[2] != byte.MaxValue)
                                    index_bytes[2]++;
                                else
                                {
                                    index_bytes[3]++;
                                    index_bytes[2] = 0;
                                }
                                index_bytes[1] = 0;
                            }
                            index_bytes[0] = 0;
                        }
                        is_equal = true;
                        for (int i = 0; i < depth; i++)
                            if (index_bytes[depth - i - 1] != data[cf * size + offset + 4 - i - 1])
                            {
                                is_equal = false;
                                break;
                            }
                    } while (!is_equal);
                }
            }
            for (uint i = index_id; i < index_size; i++)
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
            int depth = (int)Math.Log(index.Length, 256);
            bool is_zero = true;
            for (int i = 0; i < depth; i++)
                if (obj[obj.Length - i - 1] != 0)
                {
                    is_zero = false;
                    break;
                }
            byte[] index_index_bin = new byte[4];
            Buffer.BlockCopy(obj, obj.Length - depth, index_index_bin, 0, depth);
            int index_index = BitConverter.ToInt32(index_index_bin, 0);
            if (is_zero)
                left = 0;
            else
                left = (int)index[index_index - 1];
            right = (int)index[index_index] - 1;
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
        public static int GetIndex(byte[] data, int size, int offset, byte[] obj, uint[] index)
        {
            int start;
            int stop;
            BinarySearch(data, size, offset, obj, out start, out stop, index);
            if (stop - start > 0)
                return start; //throw new Exception("Collision detected!");
            else if (start == stop)
                return start;
            return -1;
        }
        public static int GetIndex(byte[] data, int size, int offset, byte[] obj)
        {
            int start = 0;
            int stop = data.Length / size;
            BinarySearch(data, size, offset, obj, ref start, ref stop);
            if (stop - start > 0)
                throw new Exception("Collision detected!");
            else if (start == stop)
                return start;
            return -1;
        }
    }
}

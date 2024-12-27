using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WatchDog_Background
{
    internal class Program
    {
        [MTAThread]
        static void Main(string[] args)
        {
            ProcessManage processManage = ProcessManage.GetInstance();

            while (true) { }
        }
    }
}

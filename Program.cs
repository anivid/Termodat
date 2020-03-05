using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO.Ports;
using System.IO;
using System.Text.RegularExpressions;

namespace Termodat
{
    class Program
    {
        static void Main(string[] args)
        {
            var termodat = new Termodat("COM4", 2, 1, 1, 1);
            ShowTitle();
            InitProgram(termodat);
        }

        private static void ShowTitle()
        {
            Console.WriteLine("Termodat control pannel\n\n" +
                              "Please input command:\n\n" +
                              //"- \"set-start-program\" to set startup program and step\n" +
                              "- \"set-program\" to configure regulatory programs\n" +
                              "- \"temp\" to show current temperature\n" +
                              "- \"setpoint\" to show current set-point\n" +
                              "- \"channel\" to show amount of termodat channels\n" +
                              "- \"process\" to show current state process\n" +
                              "- \"program\" to show current state program\n" +
                              "- \"start\" to start process\n" +                              
                              "- \"stop\" to stop termodat\n" +
                              "- \"pause\" to pause termodat\n" +
                              "- \"clear\" to clear screen\n" +
                              "- \"q or quit\" to exit\n"); ;
        }

        static void InitProgram(Termodat termodat)
        {                        
            while (true && termodat.IsConnect)
            {
                Console.Write("$ ");
                string input = Console.ReadLine();
                switch (input)
                {
                    case "set-program":
                        SetStartProgram(termodat);
                        break;
                    case "temp":
                        Show(termodat.Temp);
                        break;
                    case "setpoint":
                        Show(termodat.SetPoint);
                        break;
                    case "channel":
                        Show(termodat.Channels);
                        break;
                    case "process":
                        Show(termodat.StateProcess.ToString());
                        break;
                    case "program":
                        Show(termodat.StateProgram.ToString());
                        break;
                    case "start":
                        if (termodat.StateProcess != StateProcess.Run)
                        {
                            termodat.StartProcess();
                            Show("Start OK");
                        }
                        else
                            Show("Process already running");
                        break;
                    case "stop":
                        if (termodat.StateProcess != StateProcess.Stop)
                        {
                            termodat.StopProcess();
                            Show("Stop OK");
                        }
                        else
                            Show("Process already stopped");                        
                        break;
                    case "pause":
                        if (termodat.StateProcess != StateProcess.Pause)
                        {
                            termodat.PauseProcess();
                            Show("Pause OK");
                        }
                        else
                            Show("Process already paused");
                        break;
                    case "clear":
                        Console.Clear();
                        ShowTitle();
                        break;
                    case "q":
                    case "quit":
                        Show("Bye...");
                        return;


                    default:
                        Console.WriteLine("Incorrect input, try again");
                        break;

                }
            }
        }
        static void SetStartProgram(Termodat termodat)
        {
            bool cicle = true;
            Console.WriteLine("Editable program number: ");
            int programNumber = int.Parse(Console.ReadLine());
            Console.WriteLine("Editable step number: ");
            int stepNumber = int.Parse(Console.ReadLine());
            Console.WriteLine("What doing in this step, \"HeatingOrCooling\", \"Exposure\", \"Goto\", \"Stop\"?: ");
            string step = Console.ReadLine();
            StateProgram state = StateProgram.HeatingOrCooling;
            while (cicle)
            {
                switch (step)
                {
                    case "HeatingOrCooling":
                        state = StateProgram.HeatingOrCooling;
                        cicle = false;
                        break;
                    case "Exposure":
                        state = StateProgram.Exposure;
                        cicle = false;
                        break;
                    case "Goto":
                        state = StateProgram.Goto;
                        cicle = false;
                        break;
                    case "Stop":
                        state = StateProgram.Stop;
                        cicle = false;
                        break;
                    default:
                        Console.WriteLine("Incorrect input, try again");
                        break;
                }
            }
            cicle = true;

            Console.WriteLine("Параметр 1 (время выдержки, либо скорость (0,1ºC/ч), либо номер программы (если Goto)) Integer (100 = 10,0): ");
            int param1 = int.Parse(Console.ReadLine());
            Console.WriteLine("Параметр 2 (целевая уставка в 0,1ºC)] Integer: ");
            int param2 = int.Parse(Console.ReadLine());
            Console.WriteLine("Условие перехода на следующий шаг \"Tcalc\", \"ManualAccept\", \"Tmeasure\"");
            TransitionCondition condition = TransitionCondition.Tmeasure;
            string cond = Console.ReadLine();
            while (cicle)
            {
                switch (cond)
                {
                    case "Tcalc":
                        condition = TransitionCondition.Tcalc;
                        cicle = false;
                        break;
                    case "ManualAccept":
                        condition = TransitionCondition.ManualAccept;
                        cicle = false;
                        break;
                    case "Tmeasure":
                        condition = TransitionCondition.Tmeasure;
                        cicle = false;
                        break;
                    default:
                        Console.WriteLine("Incorrect input, try again");
                        break;
                }
            }
            termodat.SetupProgram(programNumber, stepNumber, state, param1, param2, condition);
        }
        static void Show(object obj)
        {
            Console.WriteLine($">> {obj}\n");
        }

        #region Methods for working with bytes
        public static byte[] ReadHoldingRegister(int id, int startAddress, int length)
        {
            byte[] data = new byte[8];

            byte High, Low;
            data[0] = Convert.ToByte(id);
            data[1] = Convert.ToByte(3);
            byte[] _adr = BitConverter.GetBytes(startAddress - 1);
            data[2] = _adr[1];
            data[3] = _adr[0];
            byte[] _length = BitConverter.GetBytes(length);
            data[4] = _length[1];
            data[5] = _length[0];
            myCRC(data, 6, out High, out Low);
            data[6] = Low;
            data[7] = High;
            return data;
        }
        public static void myCRC(byte[] message, int length, out byte CRCHigh, out byte CRCLow)
        {
            ushort CRCFull = 0xFFFF;
            for (int i = 0; i < length; i++)
            {
                CRCFull = (ushort)(CRCFull ^ message[i]);
                for (int j = 0; j < 8; j++)
                {
                    if ((CRCFull & 0x0001) == 0)
                        CRCFull = (ushort)(CRCFull >> 1);
                    else
                    {
                        CRCFull = (ushort)((CRCFull >> 1) ^ 0xA001);
                    }
                }
            }
            CRCHigh = (byte)((CRCFull >> 8) & 0xFF);
            CRCLow = (byte)(CRCFull & 0xFF);
        }
        static byte[] ReadExact(Stream s, int nbytes)
        {
            var buf = new byte[nbytes];
            var readpos = 0;
            while (readpos < nbytes)
                readpos += s.Read(buf, readpos, nbytes - readpos);
            return buf;
        }
        #endregion
    }
}

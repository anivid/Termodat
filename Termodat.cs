using System;
using System.IO;
using System.IO.Ports;

namespace Termodat
{
    public enum StateProgarm
    {
        HeatingOrCooling = 0,
        Exposure = 1,
        Goto = 2,
        Stop = 3
    }
    public enum StateProcess
    {
        Run,
        Stop,
        Pause,
        Undefined
    }

    public class Termodat
    {
        private SerialPort _serial;
        private readonly int _devAddress;
        private readonly int _channel;
        private const int _baudrate = 57600;
        private const int _dataBits = 8;

        public int Channels { get; private set; }
        public double Temp //текущая температура 0x0170
        {
            get
            {
                return getDoubleValue(0x0170, 4);
            }
            private set { }
        }
        public double SetPoint //текущая уставка 0x0173
        {
            get
            {
                return getDoubleValue(0x0173, 1);
            }
            private set { }
        }
        public int NProgram { get; private set; } = 0; //Номер программы регулирования, с которой запускать процесс 0x017b
        public int NStepProgram { get; private set; } //Номер шага программы регулирования, с которого запускать процесс 0x017c        
        public int TimeLeftStand //Оставшееся время выдержки, мин. 0x0178
        {
            get
            {
                return getIntValue(0x0178, 1);
            }
            private set {}
        } 
        public string Log { get; private set; }
        public StateProcess StateProcess { get; private set; } = StateProcess.Undefined;

        public Termodat(string COMPort, int devAddress, int channel = 1, int nProg = 1, int nStep = 1)
        {
            _serial = new SerialPort(COMPort)
            {
                BaudRate = _baudrate,
                Parity = Parity.None,
                StopBits = StopBits.One,
                DataBits = _dataBits
                //DataBits = _dataBits
            };
            _devAddress = devAddress;

            Channels = getIntValue(0x0130, 1);

            if (channel>Channels || channel<1) throw new ArgumentOutOfRangeException($"***Number of termodat channel must be in range [1-{Channels}]***");
            else _channel = channel;

            this.SetStartupSettings(nProg, nStep);            
        }

        public void StartProcess(int nProg, int nStep) //0x0180 val 1
        {
            this.SetStartupSettings(nProg, nStep);
            this.Set(0x180, 1);
            StateProcess = StateProcess.Run;
        }
        public void StartProcess() //перегрузка, процесс запустится с ранее заданным номером программы 
        {
            this.Set(0x180, 1);
            StateProcess = StateProcess.Run;
        }
        public void PauseProcess() //0x0180 val 2
        {
            this.Set(0x180, 1);
            StateProcess = StateProcess.Pause;
        }
        public void Set(long registerAddress, long value)
        {
            //byte[] bytes = new byte[20];
            string response = String.Empty; //в принимаемых аргументах раньше был out string response для отладки. удалим в конечном релизе
            string packet = generateWritePacket(_devAddress,_channel, registerAddress, value);
            _serial.Open();
            _serial.Write(packet);
            try
            {
                response = _serial.ReadLine();  //должно вернуться то же, что записали, packet              
            }
            catch (Exception ex)
            {
                _serial.Close();
                this.LoggingException(ex);
            }
            _serial.Close();
            //if (response == packet.Remove(packet.Length-1,1)) return true; //не возвращается <LF>
            //else return false;            
        }

        #region Private methods        
        private void SetStartupSettings (int nProg, int nStep)
        {
            NProgram = nProg; NStepProgram = nStep;
            this.Set(0x017b, nProg - 1);
            this.Set(0x017c, nStep - 1);
        }

        #region Getting values
        private int getIntValue(long registerAddress, long amountOfVals)
        {
            int res = -1;
            _serial.Open();            
            _serial.Write(generateReadPacket(_devAddress, _channel, registerAddress, amountOfVals)); 
            try
            {
                string response = _serial.ReadLine();
                res = intParse(response);
            }
            catch (Exception ex)
            {
                _serial.Close();
                this.LoggingException(ex);
            }
            _serial.Close();
            return res;
        }
        private double getDoubleValue(long registerAddress, long value)
        {
            //запрос на получение значения тепмературы (например: значения текущей температуры или значение уставки)
            double res = -1;
            _serial.Open();
            //_serial.Write(generateReadPacket(_devAddress, _channel, 0x0170, 1)); //default. Make registerAddress from enum?
            _serial.Write(generateReadPacket(_devAddress, _channel, registerAddress, value)); //
            try
            {
                res = doubleParse(_serial.ReadLine());
            }
            catch (Exception ex)
            {
                _serial.Close();
                this.LoggingException(ex);
            }
            _serial.Close();
            return res;
        }
        #endregion

        #region Generate Packets
        private string generateWritePacket(int slaveAddress, long registerAddress, long value)
        {

            int lrc = 0;
            int sum1, sum2, sum3, sum4, sum5, sum6;
            String packet = ":";

            packet = packet + slaveAddress.ToString("X2") + "06" + registerAddress.ToString("X4") + value.ToString("X4");
            String sub1 = packet.Substring(1, 2); sum1 = Convert.ToInt16(sub1, 16);
            String sub2 = packet.Substring(3, 2); sum2 = Convert.ToInt16(sub2, 16);

            String sub3 = packet.Substring(5, 2); sum3 = Convert.ToInt16(sub3, 16);
            String sub4 = packet.Substring(7, 2); sum4 = Convert.ToInt16(sub4, 16);

            String sub5 = packet.Substring(9, 2); sum5 = Convert.ToInt16(sub5, 16);
            String sub6 = packet.Substring(11, 2); sum6 = Convert.ToInt16(sub6, 16);

            //// Longitudinal redundancy check Caclulation
            lrc = sum1 + sum2 + sum3 + sum4 + sum5 + sum6; //slaveAddress + registerAddress + value;
            lrc = ~lrc; // NOT
            lrc = lrc + 1;

            String Output = packet + lrc.ToString("X2").Substring(6, 2) + "\r\n";
            return Output;
        }
        private string generateWritePacket(int slaveAddress, long numberOfChannel, long registerAddress, long value) //для регистров в которых учитывается канал
        {

            int lrc = 0;
            int sum1, sum2, sum3, sum4, sum5, sum6;
            String packet = ":";

            packet = packet + slaveAddress.ToString("X2") + "06" + registerAddress.ToString("X4") + value.ToString("X4");
            String sub1 = packet.Substring(1, 2); sum1 = Convert.ToInt16(sub1, 16);
            String sub2 = packet.Substring(3, 2); sum2 = Convert.ToInt16(sub2, 16);

            String sub3 = packet.Substring(5, 2); sum3 = Convert.ToInt16(sub3, 16);
            String sub4 = packet.Substring(7, 2); sum4 = Convert.ToInt16(sub4, 16);

            String sub5 = packet.Substring(9, 2); sum5 = Convert.ToInt16(sub5, 16);
            String sub6 = packet.Substring(11, 2); sum6 = Convert.ToInt16(sub6, 16);

            //// Longitudinal redundancy check Caclulation
            lrc = sum1 + sum2 + sum3 + sum4 + sum5 + sum6; //slaveAddress + registerAddress + value;
            lrc = ~lrc; // NOT
            lrc = lrc + 1;

            String Output = packet + lrc.ToString("X2").Substring(6, 2) + "\r\n";
            return Output;
        }
        private string generateReadPacket(int slaveAddress, long registerAddress, long numberOfRegistersToRead)
        {

            long value = 1;
            value = numberOfRegistersToRead;
            int lrc = 0;
            int sum1, sum2, sum3, sum4, sum5, sum6;
            String packet = ":";

            packet = packet + slaveAddress.ToString("X2") + "03" + registerAddress.ToString("X4") + value.ToString("X4");
            String sub1 = packet.Substring(1, 2); sum1 = Convert.ToInt16(sub1, 16);// int.TryParse(sub1, out sum1);
            String sub2 = packet.Substring(3, 2); sum2 = Convert.ToInt16(sub2, 16);

            String sub3 = packet.Substring(5, 2); sum3 = Convert.ToInt16(sub3, 16);
            String sub4 = packet.Substring(7, 2); sum4 = Convert.ToInt16(sub4, 16);

            String sub5 = packet.Substring(9, 2); sum5 = Convert.ToInt16(sub5, 16);
            String sub6 = packet.Substring(11, 2); sum6 = Convert.ToInt16(sub6, 16);

            //// Longitudinal redundancy check Caclulation
            lrc = sum1 + sum2 + sum3 + sum4 + sum5 + sum6; //slaveAddress + registerAddress + value;
            lrc = ~lrc; // NOT
            lrc = lrc + 1;


            String Output = packet + lrc.ToString("X2").Substring(6, 2) + "\r\n";
            return Output;
        }
        private string generateReadPacket(int slaveAddress, long numberOfChannel, long registerAddress, long numberOfRegistersToRead)//для регистров в которых учитывается канал
        {
            switch (numberOfChannel)
            {
                case 1:
                    break;
                case 2:
                    registerAddress += 0x0400;
                    break;
                case 3:
                    registerAddress += 0x0800;
                    break;
                case 4:
                    registerAddress += 0x0c00;
                    break;
            }

            long value = 1;
            value = numberOfRegistersToRead;
            int lrc = 0;
            int sum1, sum2, sum3, sum4, sum5, sum6;
            String packet = ":";

            packet = packet + slaveAddress.ToString("X2") + "03" + registerAddress.ToString("X4") + value.ToString("X4");
            String sub1 = packet.Substring(1, 2); sum1 = Convert.ToInt16(sub1, 16);// int.TryParse(sub1, out sum1);
            String sub2 = packet.Substring(3, 2); sum2 = Convert.ToInt16(sub2, 16);

            String sub3 = packet.Substring(5, 2); sum3 = Convert.ToInt16(sub3, 16);
            String sub4 = packet.Substring(7, 2); sum4 = Convert.ToInt16(sub4, 16);

            String sub5 = packet.Substring(9, 2); sum5 = Convert.ToInt16(sub5, 16);
            String sub6 = packet.Substring(11, 2); sum6 = Convert.ToInt16(sub6, 16);

            //// Longitudinal redundancy check Caclulation
            lrc = sum1 + sum2 + sum3 + sum4 + sum5 + sum6; //slaveAddress + registerAddress + value;
            lrc = ~lrc; // NOT
            lrc = lrc + 1;


            String Output = packet + lrc.ToString("X2").Substring(6, 2) + "\r\n";
            return Output;
        }
        #endregion
        
        #region Parsing
        private int intParse(string HexResponse)
        {
            HexResponse = HexResponse.Substring(7, 4);
            return Convert.ToInt32(HexResponse, 16);
        }
        private double doubleParse(string HexResponse)
        {
            HexResponse = intParse(HexResponse).ToString();
            HexResponse = HexResponse.Insert(HexResponse.Length - 1, ",");

            return double.Parse(HexResponse);
        }
        #endregion
        #endregion
        private void LoggingException(Exception ex)
        {
            Log = DateTime.Now + ex.Message;
            File.AppendAllText(@"log.txt", Log+"\n");
        }
        private long generateLongFromDouble(double value)
        {
            //термодат интерпретирует double как целое значение,
            //например, если передать значение 112, термодат поймет
            //это как 11.2; 110 как 11.0.
            string val = value.ToString();
            if (val.Contains(",")) val = val.Remove(val.IndexOf(","), 1);
            return long.Parse(val);
            //return res.ToString("X2");
        }
    }
}

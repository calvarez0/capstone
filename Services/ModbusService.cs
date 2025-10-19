using System;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;

namespace WPF_GUI.Services
{
    public class ModbusService : IDisposable
    {
        private SerialPort _serialPort;
        private bool _isConnected;
        private byte _slaveAddress;

        public bool IsConnected => _isConnected && _serialPort != null && _serialPort.IsOpen;

        public event EventHandler<string> ConnectionStatusChanged;
        public event EventHandler<string> ErrorOccurred;

        public ModbusService()
        {
            _isConnected = false;
        }

        public bool OpenPort(string portName, int baudRate, Parity parity, StopBits stopBits, byte modbusAddress)
        {
            try
            {
                ClosePort();

                _slaveAddress = modbusAddress;
                _serialPort = new SerialPort
                {
                    PortName = $"COM{portName}",
                    BaudRate = baudRate,
                    Parity = parity,
                    DataBits = 8,
                    StopBits = stopBits,
                    ReadTimeout = 1000,
                    WriteTimeout = 1000
                };

                _serialPort.Open();
                _isConnected = true;
                ConnectionStatusChanged?.Invoke(this, $"Connected to COM{portName} ({baudRate} baud, {parity}, {stopBits} stop bit) - Modbus Address: {modbusAddress}");
                return true;
            }
            catch (Exception ex)
            {
                _isConnected = false;
                ErrorOccurred?.Invoke(this, $"Failed to open port: {ex.Message}");
                return false;
            }
        }

        public void ClosePort()
        {
            try
            {
                if (_serialPort != null && _serialPort.IsOpen)
                {
                    _serialPort.Close();
                }
                _isConnected = false;
                ConnectionStatusChanged?.Invoke(this, "Disconnected");
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, $"Error closing port: {ex.Message}");
            }
        }

        // Function Code 03: Read Holding Registers
        public ushort[] ReadHoldingRegisters(ushort startAddress, ushort numberOfRegisters)
        {
            if (!IsConnected)
            {
                ErrorOccurred?.Invoke(this, "Not connected to device");
                return null;
            }

            try
            {
                byte[] request = BuildReadHoldingRegistersRequest(startAddress, numberOfRegisters);
                byte[] response = SendModbusRequest(request, 5 + numberOfRegisters * 2);

                if (response != null && ValidateCRC(response))
                {
                    return ParseReadHoldingRegistersResponse(response, numberOfRegisters);
                }
                else
                {
                    ErrorOccurred?.Invoke(this, "CRC check failed");
                    return null;
                }
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, $"Read error: {ex.Message}");
                return null;
            }
        }

        // Function Code 05: Force Single Coil
        public bool WriteSingleCoil(ushort coilAddress, bool value)
        {
            if (!IsConnected)
            {
                ErrorOccurred?.Invoke(this, "Not connected to device");
                return false;
            }

            try
            {
                byte[] request = BuildWriteSingleCoilRequest(coilAddress, value);
                byte[] response = SendModbusRequest(request, 8);

                return response != null && ValidateCRC(response);
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, $"Write coil error: {ex.Message}");
                return false;
            }
        }

        // Function Code 06: Preset Single Register
        public bool WriteSingleRegister(ushort registerAddress, ushort value)
        {
            if (!IsConnected)
            {
                ErrorOccurred?.Invoke(this, "Not connected to device");
                return false;
            }

            try
            {
                byte[] request = BuildWriteSingleRegisterRequest(registerAddress, value);
                byte[] response = SendModbusRequest(request, 8);

                return response != null && ValidateCRC(response);
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, $"Write register error: {ex.Message}");
                return false;
            }
        }

        // Function Code 16: Preset Multiple Registers
        public bool WriteMultipleRegisters(ushort startAddress, ushort[] values)
        {
            if (!IsConnected)
            {
                ErrorOccurred?.Invoke(this, "Not connected to device");
                return false;
            }

            try
            {
                byte[] request = BuildWriteMultipleRegistersRequest(startAddress, values);
                byte[] response = SendModbusRequest(request, 8);

                return response != null && ValidateCRC(response);
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, $"Write multiple registers error: {ex.Message}");
                return false;
            }
        }

        private byte[] SendModbusRequest(byte[] request, int expectedResponseLength)
        {
            try
            {
                _serialPort.DiscardInBuffer();
                _serialPort.DiscardOutBuffer();

                _serialPort.Write(request, 0, request.Length);
                Thread.Sleep(50); // Wait for device to respond

                byte[] response = new byte[expectedResponseLength];
                int bytesRead = _serialPort.Read(response, 0, expectedResponseLength);

                if (bytesRead == expectedResponseLength)
                {
                    return response;
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        private byte[] BuildReadHoldingRegistersRequest(ushort startAddress, ushort numberOfRegisters)
        {
            byte[] request = new byte[8];
            request[0] = _slaveAddress;
            request[1] = 0x03; // Function code
            request[2] = (byte)(startAddress >> 8);
            request[3] = (byte)(startAddress & 0xFF);
            request[4] = (byte)(numberOfRegisters >> 8);
            request[5] = (byte)(numberOfRegisters & 0xFF);

            ushort crc = CalculateCRC(request, 6);
            request[6] = (byte)(crc & 0xFF);
            request[7] = (byte)(crc >> 8);

            return request;
        }

        private byte[] BuildWriteSingleCoilRequest(ushort coilAddress, bool value)
        {
            byte[] request = new byte[8];
            request[0] = _slaveAddress;
            request[1] = 0x05; // Function code
            request[2] = (byte)(coilAddress >> 8);
            request[3] = (byte)(coilAddress & 0xFF);
            request[4] = value ? (byte)0xFF : (byte)0x00;
            request[5] = 0x00;

            ushort crc = CalculateCRC(request, 6);
            request[6] = (byte)(crc & 0xFF);
            request[7] = (byte)(crc >> 8);

            return request;
        }

        private byte[] BuildWriteSingleRegisterRequest(ushort registerAddress, ushort value)
        {
            byte[] request = new byte[8];
            request[0] = _slaveAddress;
            request[1] = 0x06; // Function code
            request[2] = (byte)(registerAddress >> 8);
            request[3] = (byte)(registerAddress & 0xFF);
            request[4] = (byte)(value >> 8);
            request[5] = (byte)(value & 0xFF);

            ushort crc = CalculateCRC(request, 6);
            request[6] = (byte)(crc & 0xFF);
            request[7] = (byte)(crc >> 8);

            return request;
        }

        private byte[] BuildWriteMultipleRegistersRequest(ushort startAddress, ushort[] values)
        {
            int length = 9 + values.Length * 2;
            byte[] request = new byte[length];

            request[0] = _slaveAddress;
            request[1] = 0x10; // Function code 16
            request[2] = (byte)(startAddress >> 8);
            request[3] = (byte)(startAddress & 0xFF);
            request[4] = (byte)(values.Length >> 8);
            request[5] = (byte)(values.Length & 0xFF);
            request[6] = (byte)(values.Length * 2);

            for (int i = 0; i < values.Length; i++)
            {
                request[7 + i * 2] = (byte)(values[i] >> 8);
                request[8 + i * 2] = (byte)(values[i] & 0xFF);
            }

            ushort crc = CalculateCRC(request, length - 2);
            request[length - 2] = (byte)(crc & 0xFF);
            request[length - 1] = (byte)(crc >> 8);

            return request;
        }

        private ushort[] ParseReadHoldingRegistersResponse(byte[] response, ushort numberOfRegisters)
        {
            ushort[] values = new ushort[numberOfRegisters];

            for (int i = 0; i < numberOfRegisters; i++)
            {
                values[i] = (ushort)((response[3 + i * 2] << 8) | response[4 + i * 2]);
            }

            return values;
        }

        private ushort CalculateCRC(byte[] data, int length)
        {
            ushort crc = 0xFFFF;

            for (int i = 0; i < length; i++)
            {
                crc ^= data[i];

                for (int j = 0; j < 8; j++)
                {
                    if ((crc & 0x0001) != 0)
                    {
                        crc >>= 1;
                        crc ^= 0xA001;
                    }
                    else
                    {
                        crc >>= 1;
                    }
                }
            }

            return crc;
        }

        private bool ValidateCRC(byte[] data)
        {
            if (data == null || data.Length < 3)
                return false;

            ushort receivedCRC = (ushort)((data[data.Length - 1] << 8) | data[data.Length - 2]);
            ushort calculatedCRC = CalculateCRC(data, data.Length - 2);

            return receivedCRC == calculatedCRC;
        }

        public void Dispose()
        {
            ClosePort();
            _serialPort?.Dispose();
        }
    }
}

using System;

namespace ModbusActuatorControl
{
    // Common interface for Modbus master implementations (hardware and simulation)
    public interface IActuatorMaster : IDisposable
    {
        bool IsConnected { get; }

        void Connect();
        void Disconnect();

        ushort[] ReadHoldingRegisters(byte slaveId, ushort startAddress, ushort count);
        void WriteSingleRegister(byte slaveId, ushort address, ushort value);
        bool[] ReadCoils(byte slaveId, ushort startAddress, ushort count);
        void WriteSingleCoil(byte slaveId, ushort address, bool value);
    }
}

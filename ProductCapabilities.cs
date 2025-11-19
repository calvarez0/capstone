using System;
using System.Collections.Generic;
using System.Linq;

namespace ModbusActuatorControl
{
    // Helper class to determine what registers and bits are available for each product type
    public static class ProductCapabilities
    {
        // Product identifiers
        public const ushort PRODUCT_S7X = 0x8000;
        public const ushort PRODUCT_EHO = 0x8001;
        public const ushort PRODUCT_NOVA = 0x8002;

        // Get product name from identifier
        public static string GetProductName(ushort productId)
        {
            return productId switch
            {
                PRODUCT_S7X => "S7X",
                PRODUCT_EHO => "EHO",
                PRODUCT_NOVA => "Nova",
                _ => $"Unknown (0x{productId:X4})"
            };
        }

        // Check if an entire register is available for a product
        public static bool IsRegisterAvailable(ushort productId, int register)
        {
            return productId switch
            {
                PRODUCT_EHO => !IsEHORegisterUnavailable(register),
                PRODUCT_S7X => !IsS7XRegisterUnavailable(register),
                PRODUCT_NOVA => true, // Nova has all registers
                _ => true // Unknown products get full access
            };
        }

        // Check if a specific bit in a register is available
        public static bool IsBitAvailable(ushort productId, int register, int bit)
        {
            // First check if entire register is unavailable
            if (!IsRegisterAvailable(productId, register))
                return false;

            return productId switch
            {
                PRODUCT_EHO => !IsEHOBitUnavailable(register, bit),
                PRODUCT_S7X => !IsS7XBitUnavailable(register, bit),
                PRODUCT_NOVA => !IsNovaBitUnavailable(register, bit),
                _ => true
            };
        }

        // Get list of available bits for a register
        public static List<int> GetAvailableBits(ushort productId, int register, int totalBits = 16)
        {
            var availableBits = new List<int>();
            for (int bit = 0; bit < totalBits; bit++)
            {
                if (IsBitAvailable(productId, register, bit))
                {
                    availableBits.Add(bit);
                }
            }
            return availableBits;
        }

        // EHO unavailable entire registers
        private static bool IsEHORegisterUnavailable(int register)
        {
            return register switch
            {
                24 => true,
                26 => true,
                28 => true,
                103 => true,
                104 => true,
                105 => true,
                106 => true,
                112 => true,
                113 => true,
                114 => true,
                115 => true,
                500 => true,
                501 => true,
                502 => true,
                503 => true,
                504 => true,
                505 => true,
                506 => true,
                507 => true,
                _ => false
            };
        }

        // EHO unavailable bits (when register is partially available)
        private static bool IsEHOBitUnavailable(int register, int bit)
        {
            return (register, bit) switch
            {
                (0, 1) => true,
                (0, 3) => true,
                (1, 15) => true,
                (2, 0) => true,
                (2, 15) => true,
                (3, 2) => true,
                (3, 3) => true,
                (3, 10) => true,
                (4, 0) => true,
                (4, 1) => true,
                (4, 2) => true,
                (4, 3) => true,
                (4, 5) => true,
                (4, 6) => true,
                (4, 7) => true,
                (4, 8) => true,
                (4, 11) => true,
                (11, 6) => true,
                (11, 8) => true,
                (11, 14) => true,
                (11, 15) => true,
                (12, 0) => true,
                (12, 1) => true,
                (12, 2) => true,
                (12, 4) => true,
                (12, 5) => true,
                (12, 7) => true,
                (12, 8) => true,
                (12, 9) => true,
                (12, 10) => true,
                (12, 11) => true,
                (12, 12) => true,
                (12, 13) => true,
                _ => false
            };
        }

        // Additional check for Register 107 LH (lower half) for EHO
        public static bool IsRegister107LowerHalfAvailable(ushort productId)
        {
            return productId switch
            {
                PRODUCT_EHO => false,
                PRODUCT_S7X => false,
                _ => true
            };
        }

        // S7X unavailable entire registers
        private static bool IsS7XRegisterUnavailable(int register)
        {
            return register switch
            {
                16 => true,
                28 => true,
                29 => true,
                105 => true,
                106 => true,
                502 => true,
                503 => true,
                506 => true,
                507 => true,
                _ => false
            };
        }

        // S7X unavailable bits
        private static bool IsS7XBitUnavailable(int register, int bit)
        {
            return (register, bit) switch
            {
                (0, 14) => true,
                (4, 4) => true,
                (4, 5) => true,
                (4, 6) => true,
                (4, 7) => true,
                (4, 8) => true,
                (4, 11) => true,
                (4, 12) => true,
                (4, 13) => true,
                (10, 3) => true,
                (10, 4) => true,
                (11, 0) => true,
                (11, 3) => true,
                (11, 6) => true,
                (11, 8) => true,
                (11, 11) => true,
                (11, 12) => true,
                (11, 13) => true,
                (12, 2) => true,
                (12, 4) => true,
                (12, 5) => true,
                (12, 6) => true,
                (12, 7) => true,
                (12, 8) => true,
                (12, 9) => true,
                (12, 10) => true,
                (12, 11) => true,
                _ => false
            };
        }

        // Nova unavailable bits (minimal restrictions)
        private static bool IsNovaBitUnavailable(int register, int bit)
        {
            return (register, bit) switch
            {
                (0, 14) => true,
                (3, 10) => true,
                (11, 0) => true,
                _ => false
            };
        }
    }
}

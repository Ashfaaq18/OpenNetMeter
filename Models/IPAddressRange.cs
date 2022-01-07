using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace WhereIsMyData.Models
{
    class IPAddressRange
    {
        private readonly AddressFamily addressFamily;
        private readonly byte[] lowerBytes;
        private readonly byte[] upperBytes;

        public IPAddressRange(IPAddress lowerInclusive, IPAddress upperInclusive)
        {
            addressFamily = lowerInclusive.AddressFamily;
            lowerBytes = lowerInclusive.GetAddressBytes();
            upperBytes = upperInclusive.GetAddressBytes();
        }

        public bool IsInRange(IPAddress address)
        {
            if (address.AddressFamily != addressFamily)
            {
                return false;
            }

            byte[] addressBytes = address.GetAddressBytes();

            bool lowerBoundary = true, upperBoundary = true;

            for (int i = 0; i < lowerBytes.Length &&
                (lowerBoundary || upperBoundary); i++)
            {
                if ((lowerBoundary && addressBytes[i] < lowerBytes[i]) ||
                    (upperBoundary && addressBytes[i] > upperBytes[i]))
                {
                    return false;
                }

                lowerBoundary &= (addressBytes[i] == lowerBytes[i]);
                upperBoundary &= (addressBytes[i] == upperBytes[i]);
            }
            return true;
        }

    }
}

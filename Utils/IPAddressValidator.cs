using System.Net;

namespace WebCountry.Utils
{
    public static class IPAddressValidator
    {
        /// <summary>
        /// Validates if an IP address is suitable for geolocation lookup
        /// </summary>
        /// <param name="ipAddress">IP address string to validate</param>
        /// <param name="parsedIP">Parsed IP address if valid</param>
        /// <param name="errorMessage">Error message if invalid</param>
        /// <returns>True if valid for geolocation lookup</returns>
        public static bool IsValidForGeolocation(string ipAddress, out IPAddress? parsedIP, out string? errorMessage)
        {
            parsedIP = null;
            errorMessage = null;

            // Check if IP address can be parsed
            if (!IPAddress.TryParse(ipAddress, out parsedIP))
            {
                errorMessage = "Invalid IP address format";
                return false;
            }

            // Check for private addresses
            if (IsPrivateAddress(parsedIP))
            {
                errorMessage = "Private IP addresses do not have geolocation data";
                return false;
            }

            // Check for loopback addresses
            if (IPAddress.IsLoopback(parsedIP))
            {
                errorMessage = "Loopback addresses do not have geolocation data";
                return false;
            }

            // Check for link-local addresses
            if (IsLinkLocal(parsedIP))
            {
                errorMessage = "Link-local addresses do not have geolocation data";
                return false;
            }

            // Check for multicast addresses
            if (IsMulticast(parsedIP))
            {
                errorMessage = "Multicast addresses do not have geolocation data";
                return false;
            }

            // Check for broadcast and special addresses
            if (IsSpecialUseAddress(parsedIP))
            {
                errorMessage = "Special use addresses do not have geolocation data";
                return false;
            }

            return true;
        }

        /// <summary>
        /// Checks if an IP address is a private address (RFC 1918)
        /// </summary>
        private static bool IsPrivateAddress(IPAddress ipAddress)
        {
            if (ipAddress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
            {
                var bytes = ipAddress.GetAddressBytes();

                // 10.0.0.0/8
                if (bytes[0] == 10)
                    return true;

                // 172.16.0.0/12
                if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
                    return true;

                // 192.168.0.0/16
                if (bytes[0] == 192 && bytes[1] == 168)
                    return true;
            }
            else if (ipAddress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
            {
                // IPv6 private addresses
                var bytes = ipAddress.GetAddressBytes();

                // fc00::/7 (Unique Local Addresses)
                if ((bytes[0] & 0xfe) == 0xfc)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Checks if an IP address is a link-local address
        /// </summary>
        private static bool IsLinkLocal(IPAddress ipAddress)
        {
            if (ipAddress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
            {
                var bytes = ipAddress.GetAddressBytes();
                // 169.254.0.0/16
                return bytes[0] == 169 && bytes[1] == 254;
            }
            else if (ipAddress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
            {
                var bytes = ipAddress.GetAddressBytes();
                // fe80::/10
                return bytes[0] == 0xfe && (bytes[1] & 0xc0) == 0x80;
            }

            return false;
        }

        /// <summary>
        /// Checks if an IP address is a multicast address
        /// </summary>
        private static bool IsMulticast(IPAddress ipAddress)
        {
            if (ipAddress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
            {
                var bytes = ipAddress.GetAddressBytes();
                // 224.0.0.0/4
                return (bytes[0] & 0xf0) == 0xe0;
            }
            else if (ipAddress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
            {
                var bytes = ipAddress.GetAddressBytes();
                // ff00::/8
                return bytes[0] == 0xff;
            }

            return false;
        }

        /// <summary>
        /// Checks if an IP address is a special use address
        /// </summary>
        private static bool IsSpecialUseAddress(IPAddress ipAddress)
        {
            if (ipAddress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
            {
                var bytes = ipAddress.GetAddressBytes();

                // 0.0.0.0/8 (This network)
                if (bytes[0] == 0)
                    return true;

                // 127.0.0.0/8 (Loopback - already checked above)
                if (bytes[0] == 127)
                    return true;

                // 255.255.255.255 (Broadcast)
                if (bytes[0] == 255 && bytes[1] == 255 && bytes[2] == 255 && bytes[3] == 255)
                    return true;
            }

            return false;
        }
    }
}


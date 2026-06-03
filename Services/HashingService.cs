using System.Security.Cryptography;
using System.Text;

namespace KaijensonIventory_SalesMotorShopWeb.Services
{
    public class HashingService
    {
        public string HashData(string input)
        {
            byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
            StringBuilder builder = new();
            foreach (byte b in bytes)
                builder.Append(b.ToString("x2"));
            return builder.ToString();
        }
    }
}

using System.Security.Cryptography;
using System.Text;

namespace KaijensonIventory_SalesMotorShopWeb.Services
{
    public class HashingService
    {
        public string HashPassword(string password)
        {
            byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(password));
            return Convert.ToHexString(bytes).ToLower();
        }

        public bool VerifyPassword(string password, string hash)
        {
            return HashPassword(password) == hash;
        }
    }
}

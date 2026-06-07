using System.Security.Cryptography;
using System.Text;

namespace ARS.Classess.Utils
{
    /// <summary>
    /// Encryption utility using TripleDES with MD5 key derivation.
    /// Ported from UpSanctionScreener for secure credential storage.
    /// </summary>
    public static class Cryptor
    {
        private const string Key = "UpSL!@pAyCoL?&gt;+`";

        public static string Encrypt(string toEncrypt, bool useHashing)
        {
            byte[] keyArray;
            byte[] toEncryptArray = Encoding.UTF8.GetBytes(toEncrypt);

            if (useHashing)
            {
                using (MD5 hashmd5 = MD5.Create())
                {
                    keyArray = hashmd5.ComputeHash(Encoding.UTF8.GetBytes(Key));
                }
            }
            else
            {
                keyArray = Encoding.UTF8.GetBytes(Key);
            }

            using (TripleDES tdes = TripleDES.Create())
            {
                tdes.Key = keyArray;
                tdes.Mode = CipherMode.ECB;
                tdes.Padding = PaddingMode.PKCS7;

                using (ICryptoTransform cTransform = tdes.CreateEncryptor())
                {
                    byte[] resultArray = cTransform.TransformFinalBlock(toEncryptArray, 0, toEncryptArray.Length);
                    return Convert.ToBase64String(resultArray, 0, resultArray.Length);
                }
            }
        }

        public static string Decrypt(string cipherString, bool useHashing)
        {
            byte[] keyArray;
            byte[] toEncryptArray = Convert.FromBase64String(cipherString);

            if (useHashing)
            {
                using (var hashmd5 = MD5.Create())
                {
                    keyArray = hashmd5.ComputeHash(Encoding.UTF8.GetBytes(Key));
                }
            }
            else
            {
                keyArray = Encoding.UTF8.GetBytes(Key);
            }

            using (var tdes = TripleDES.Create())
            {
                tdes.Key = keyArray;
                tdes.Mode = CipherMode.ECB;
                tdes.Padding = PaddingMode.PKCS7;

                using (var cTransform = tdes.CreateDecryptor())
                {
                    byte[] resultArray = cTransform.TransformFinalBlock(toEncryptArray, 0, toEncryptArray.Length);
                    return Encoding.UTF8.GetString(resultArray);
                }
            }
        }
    }
}

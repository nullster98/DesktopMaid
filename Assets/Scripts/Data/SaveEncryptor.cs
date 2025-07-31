// --- START OF FILE SaveEncryptor.cs ---

using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

/// <summary>
/// AES-256 암호화를 사용하여 문자열을 암호화하고 복호화하는 헬퍼 클래스.
/// </summary>
public static class SaveEncryptor
{
    // 256비트(32바이트) 키와 128비트(16바이트) IV(초기화 벡터)를 사용합니다.
    private const int KEY_SIZE_BYTES = 32;
    private const int IV_SIZE_BYTES = 16;

    /// <summary>
    /// 평문 문자열을 암호화합니다.
    /// </summary>
    /// <param name="plainText">암호화할 문자열</param>
    /// <param name="password">암호화에 사용할 비밀번호 (여기서는 Steam ID)</param>
    /// <returns>Base64로 인코딩된 암호문</returns>
    public static string Encrypt(string plainText, string password)
    {
        try
        {
            // 비밀번호로부터 고정된 길이의 암호화 키를 생성합니다.
            byte[] keyBytes = new Rfc2898DeriveBytes(password, Encoding.UTF8.GetBytes("YourSaltHere"), 1000).GetBytes(KEY_SIZE_BYTES);

            using (Aes aes = Aes.Create())
            {
                aes.Key = keyBytes;
                aes.GenerateIV(); // 매번 새로운 IV를 생성하여 보안성을 높입니다.
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                ICryptoTransform encryptor = aes.CreateEncryptor(aes.Key, aes.IV);

                using (MemoryStream memoryStream = new MemoryStream())
                {
                    // 1. IV를 암호문 앞에 추가합니다. 복호화할 때 필요합니다.
                    memoryStream.Write(aes.IV, 0, aes.IV.Length);

                    using (CryptoStream cryptoStream = new CryptoStream(memoryStream, encryptor, CryptoStreamMode.Write))
                    {
                        byte[] plainTextBytes = Encoding.UTF8.GetBytes(plainText);
                        cryptoStream.Write(plainTextBytes, 0, plainTextBytes.Length);
                        cryptoStream.FlushFinalBlock();
                    }
                    // 2. (IV + 암호문) 전체를 Base64 문자열로 변환하여 반환합니다.
                    return Convert.ToBase64String(memoryStream.ToArray());
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveEncryptor] 암호화 실패: {e.Message}");
            return null;
        }
    }

    /// <summary>
    /// 암호문을 복호화합니다.
    /// </summary>
    /// <param name="encryptedText">Base64로 인코딩된 암호문</param>
    /// <param name="password">암호화에 사용했던 비밀번호 (여기서는 Steam ID)</param>
    /// <returns>복호화된 평문 문자열</returns>
    public static string Decrypt(string encryptedText, string password)
    {
        try
        {
            // 암호화 시와 동일한 방식으로 키를 생성해야 합니다.
            byte[] keyBytes = new Rfc2898DeriveBytes(password, Encoding.UTF8.GetBytes("YourSaltHere"), 1000).GetBytes(KEY_SIZE_BYTES);
            
            // Base64 문자열을 바이트 배열로 되돌립니다.
            byte[] cipherTextBytesWithIv = Convert.FromBase64String(encryptedText);

            using (Aes aes = Aes.Create())
            {
                aes.Key = keyBytes;
                
                // 1. 암호문 앞부분에서 IV를 다시 추출합니다.
                byte[] iv = new byte[IV_SIZE_BYTES];
                Buffer.BlockCopy(cipherTextBytesWithIv, 0, iv, 0, iv.Length);
                aes.IV = iv;

                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                ICryptoTransform decryptor = aes.CreateDecryptor(aes.Key, aes.IV);

                using (MemoryStream memoryStream = new MemoryStream(cipherTextBytesWithIv, iv.Length, cipherTextBytesWithIv.Length - iv.Length))
                {
                    using (CryptoStream cryptoStream = new CryptoStream(memoryStream, decryptor, CryptoStreamMode.Read))
                    {
                        using (StreamReader streamReader = new StreamReader(cryptoStream, Encoding.UTF8))
                        {
                            // 2. 복호화된 최종 평문을 반환합니다.
                            return streamReader.ReadToEnd();
                        }
                    }
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveEncryptor] 복호화 실패: {e.Message}. 세이브 파일이 손상되었거나 키가 다를 수 있습니다.");
            return null;
        }
    }
}
// --- END OF FILE SaveEncryptor.cs ---
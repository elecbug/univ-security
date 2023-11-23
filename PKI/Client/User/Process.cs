﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace PKI.Client.User
{
    public class Process : BaseProcess
    {
        private byte[] CaPublicKey { get; set; }
        private byte[]? PersonalPrivateKey { get; set; }

        public Process(byte[] pubKey)
            : base(new Random(DateTime.Now.Microsecond).Next(1, 1000), new TcpClient())
        {
            CaPublicKey = pubKey;
        }

        public void GetPublicKeyPair()
        {

        }

        public override void ReadMethod(string text)
        {
            string[] split = Command.Split(text);

            switch (split[2])
            {
                case Command.RecvKey:
                    {
                        int o;

                        RSA ca = RSA.Create(1024);
                        ca.ImportRSAPublicKey(CaPublicKey, out o);

                        byte[] code = Command.StringToByteArray(split[4]);

                        if (ca.VerifyData(Command.Sign, code, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1))
                        {
                            byte[] data = Command.StringToByteArray(split[3]);
                            PersonalPrivateKey = data;

                            Console.WriteLine("Verified key! Get key from CA, Your private key is [" + Command.ByteArrayToString(data) + "].");
                        }
                        else
                        {
                            Console.WriteLine("Invalid verify.");
                        }

                        break;
                    }

                case Command.RecvGetKey:
                    {
                        int o;

                        RSA ca = RSA.Create(1024);
                        ca.ImportRSAPublicKey(CaPublicKey, out o);

                        byte[] crypto = Command.StringToByteArray(split[4]);

                        if (ca.VerifyData(Command.Sign, crypto, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1))
                        {
                            KeyPair pair = new KeyPair()
                            {
                                Id = int.Parse(split[3].Split(':')[0]),
                                PublicKey = Command.StringToByteArray(split[3].Split(':')[1]),
                            };

                            KeyPairs.Add(pair);

                            Console.WriteLine("Verified key! Get key from CA, [" + Id
                                + "]'s public key is [" + Command.ByteArrayToString(pair.PublicKey) + "].");
                        }
                        else
                        {
                            Console.WriteLine("Invalid verify.");
                        }

                        break;
                    }
                case Command.RecvMsg:
                    {
                        int o;

                        RSACryptoServiceProvider rsa = new RSACryptoServiceProvider();
                        rsa.ImportRSAPrivateKey(PersonalPrivateKey, out o);

                        string msg = Encoding.UTF8.GetString(rsa.Decrypt(Command.StringToByteArray(split[3]), false));

                        Console.WriteLine("[" + split[0] + "]: " + msg);

                        byte[] code = Command.StringToByteArray(split[4]);
                        int target = int.Parse(split[0]);

                        KeyPair? pair = KeyPairs.Where(x => x.Id == target).FirstOrDefault();

                        if (pair == null)
                        {
                            Console.WriteLine("CA do not know " + target + "'s data.");

                            return;
                        }

                        RSACryptoServiceProvider p = new RSACryptoServiceProvider();
                        p.ImportRSAPublicKey(pair.PublicKey, out o);

                        if (p.VerifyData(Command.Sign, code, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1))
                        {
                            Console.WriteLine(" > This message is verified! But, you must check message time.");
                        }

                        break;
                    }
            }
        }

        public override void WriteMethod(string text)
        {
            if (text.Split(' ')[0] == Command.GetKey)
            {
                Client.GetStream()
                    .WriteAsync(Encoding.UTF8.GetBytes(Command.Create(Id, 0, Command.GetKey, text.Split(' ')[1])));
            }
            else if (text.Split(' ')[0] == Command.SendMsg)
            {
                int o;

                RSACryptoServiceProvider rsa = new RSACryptoServiceProvider();
                rsa.ImportRSAPrivateKey(PersonalPrivateKey, out o);

                byte[] sign = rsa.SignData(Command.Sign, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                int target = 0;

                try
                {
                    target = int.Parse(text.Split(' ')[1]);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Invaild ID");
                    Console.WriteLine(ex);

                    return;
                }

                KeyPair? pair = KeyPairs.Where(x => x.Id == target).FirstOrDefault();

                if (pair == null)
                {
                    Console.WriteLine("You do not know " + target + "'s data.");

                    return;
                }

                RSACryptoServiceProvider p = new RSACryptoServiceProvider();
                p.ImportRSAPublicKey(pair.PublicKey, out o);

                string result = "";

                for (int i = 2; i < text.Split(' ').Length; i++)
                {
                    result += text.Split(' ')[i] + " ";
                }

                result += " (" + DateTime.Now.ToString("yy.MM.dd HH:mm:ss") + ")";

                byte[] encry = p.Encrypt(Encoding.UTF8.GetBytes(result), false);

                Client.GetStream()
                        .Write(Encoding.UTF8.GetBytes(Command.Create(Id, target, Command.RecvMsg,
                        Command.ByteArrayToString(encry), Command.ByteArrayToString(sign))));
            }
            else
            {
                switch (text)
                {
                    case Command.GenerateKey:
                        {
                            Client.GetStream()
                                .WriteAsync(Encoding.UTF8.GetBytes(Command.Create(Id, 0, Command.GenerateKey)));
                            break;
                        }
                    default:
                        {
                            Console.WriteLine("Invalid text.");
                            break;
                        }
                }
            }
        }
    }
}

using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Channels;
using System.Xml.Linq;

namespace ConsoleApp10
{
	internal class Program
	{
		static void Main(string[] args)
		{
			string sourceString =
				@"C# (произносится си шарп) — объектно-ориентированный язык программирования общего назначения. Разработан в 1998—2001 годах группой инженеров компании Microsoft под руководством Андерса Хейлсберга и Скотта Вильтаумота[6] как язык разработки приложений для платформы Microsoft .NET Framework и .NET Core. Впоследствии был стандартизирован как ECMA-334 и ISO/IEC 23270.

C# относится к семье языков с C-подобным синтаксисом, из них его синтаксис наиболее близок к C++ и Java. Язык имеет статическую типизацию, поддерживает полиморфизм, перегрузку операторов (в том числе операторов явного и неявного приведения типа), делегаты, атрибуты, события, переменные, свойства, обобщённые типы и методы, итераторы, анонимные функции с поддержкой замыканий, LINQ, исключения, комментарии в формате XML.

Переняв многое от своих предшественников — языков C++, Delphi, Модула, Smalltalk и, в особенности, Java — С#, опираясь на практику их использования, исключает некоторые модели, зарекомендовавшие себя как проблематичные при разработке программных систем, например, C# в отличие от C++ не поддерживает множественное наследование классов (между тем допускается множественная реализация интерфейсов).

С#‎ разрабатывался как язык программирования прикладного уровня для CLR и, как таковой, зависит, прежде всего, от возможностей самой CLR. Это касается, прежде всего, системы типов С#‎, которая отражает BCL. Присутствие или отсутствие тех или иных выразительных особенностей языка диктуется тем, может ли конкретная языковая особенность быть транслирована в соответствующие конструкции CLR. Так, с развитием CLR от версии 1.1 к 2.0 значительно обогатился и сам C#; подобного взаимодействия следует ожидать и в дальнейшем (однако, эта закономерность была нарушена с выходом C# 3.0, представляющего собой расширения языка, не опирающиеся на расширения платформы .NET). CLR предоставляет С#‎, как и всем другим .NET-ориентированным языкам, многие возможности, которых лишены «классические» языки программирования. Например, сборка мусора не реализована в самом C#‎, а производится CLR для программ, написанных на C#, точно так же, как это делается для программ на VB.NET, J# и др.";

			bool[] encodedMessage = EncodeHamming(sourceString);

			(string decodedMessage, bool isCorrectDecoded, int totalErrors) = DecodeHamming(encodedMessage);

			if (isCorrectDecoded)
			{
				Console.WriteLine("Сообщение получено корректно");
			}
			else
			{
				Console.WriteLine("Сообщение получено с ошибками");
			}

			Console.WriteLine($"Количество исправленных ошибок: {totalErrors}");
		}

		public static bool[] EncodeHamming(string message)
		{
			bool[] bits = Encoding.UTF8.GetBytes(message).SelectMany(x => ByteToBits(x)).ToArray();

			List<bool[]> words = SplitIntoWords(bits, 69);

			List<bool> result = new List<bool>();
			foreach (bool[] word in words)
			{
				bool[] hammingWord = CreateHammingCode(word);
				result.AddRange(hammingWord);
			}

			bool[] controlSum = CRC16(words.SelectMany(x => x).ToArray());

			string csStr = string.Join(',', controlSum.Select(Convert.ToInt32));

			result.AddRange(controlSum);

			return result.ToArray();
		}

		public static (string, bool, int) DecodeHamming(bool[] message)
		{
			bool[] sourceCRC16 = GetCRC16Bits(message.ToArray());
			bool[] messageBits = RemoveCRC16Bits(message.ToArray());

			

			List<bool[]> words = SplitIntoWords(messageBits, 76);

			int totalErrors = 0;

			List<bool> decodedMessageBits = new List<bool>();
			try
			{
				foreach (bool[] word in words)
				{
					int errorsInWord = 0;
					int? errorIndex = null;
					bool isCorrect = false;
					bool[] decodedWord = null;

					while (!isCorrect)
					{
						(decodedWord, isCorrect, errorIndex) = DecodeHamingCode(word);

						if (!isCorrect)
						{
							totalErrors++;
							Console.WriteLine("Исправлена ошибка");
							word[errorIndex.Value] = !word[errorIndex.Value];
						}
					}

					decodedMessageBits.AddRange(decodedWord);
				}
			}
			catch (Exception)
			{
				
			}


			bool[] currentCRC16 = CRC16(decodedMessageBits.ToArray());



			byte[] bytes = BitsToBytes(decodedMessageBits.ToArray());

			return (Encoding.UTF8.GetString(bytes), IsCRC16Correct(sourceCRC16, currentCRC16), totalErrors);
		}

		private static List<bool[]> SplitIntoWords(bool[] bits, int wordLength)
		{
			List<bool[]> words = new List<bool[]>();
			for (int i = 0; i < bits.Length; i += wordLength)
			{
				int length = Math.Min(wordLength, bits.Length - i);
				words.Add(bits.Skip(i).Take(length).ToArray());
			}
			return words;
		}

		private static bool[] CreateHammingCode(bool[] word)
		{
			bool[] hamingBytes = AddEmptyControlDigits(word);

			for (int i = 1; i < hamingBytes.Length; i *= 2)
			{
				int sum = 0;
				for (int j = i-1; j < hamingBytes.Length; j += i*2)
				{
					sum += hamingBytes.Skip(j).Take(i).Sum(Convert.ToInt32);
				}

				hamingBytes[i-1] = sum % 2 == 1;
			}

			return hamingBytes;
		}


		private static (bool[], bool, int?) DecodeHamingCode(bool[] word)
		{
			bool[] wordWithEmptyControl = FillControlDigitsEmpty(word);

			int countControls = 0;
			for (int i = 1; i < wordWithEmptyControl.Length - countControls; i *= 2)
			{
				countControls++;
				int sum = 0;
				for (int j = i - 1; j < word.Length; j += i*2)
				{
					sum += wordWithEmptyControl.Skip(j).Take(i).Sum(Convert.ToInt32);
				}

				wordWithEmptyControl[i-1] = sum % 2 == 1;
			}

			bool[] wordControlDigits = GetControlDigits(word);
			bool[] testControlDigits = GetControlDigits(wordWithEmptyControl);

			int errorIndex = 0;
			for (int i = 0; i < wordControlDigits.Length; i++)
			{
				if (wordControlDigits[i] != testControlDigits[i])
				{
					errorIndex += Convert.ToInt32(Math.Pow(2, i));
				}
			}

			if (errorIndex != 0)
			{
				return (null, false, errorIndex - 1);
			}

			return (GetValueDigits(word), true, null);
		}

		private static bool[] FillControlDigitsEmpty(bool[] bytes)
		{
			bool[] bytesEmptyCotrols = new bool[bytes.Length];
			Array.Copy(bytes, bytesEmptyCotrols, bytes.Length);

			int countControls = 0;
			for (int i = 1; i < bytes.Length - countControls; i *= 2)
			{
				countControls++;
				bytesEmptyCotrols[i - 1] =  false;
			}

			return bytesEmptyCotrols;
		}


		private static bool[] GetControlDigits(bool[] bytes)
		{
			List<bool> controlDigits = new List<bool>();

			int countControls = 0;
			for (int i = 1; i < bytes.Length - countControls; i *= 2)
			{
				countControls++;
				controlDigits.Add(bytes[i - 1]);
			}

			return controlDigits.ToArray();
		}

		private static bool[] GetValueDigits(bool[] bytes)
		{
			List<bool> controlDigits = new List<bool>();

			int countControls = 0;
			for (int i = 0; i < bytes.Length; i += 1)
			{

				if (!IsPowerOfTwo(i + 1) || i >= bytes.Length - countControls)
				{
					controlDigits.Add(bytes[i]);
				}
				else
				{
					if (IsPowerOfTwo(i + 1) && i < bytes.Length - countControls)
						countControls++;
				}
			}


			return controlDigits.ToArray();
		}

		private static bool[] AddEmptyControlDigits(bool[] bytes)
		{
			List<bool> bytesList = bytes.ToList();

			for (int i = 1; i <= bytes.Length; i *= 2)
			{
				bytesList.Insert(i - 1, false);
			}

			return  bytesList.ToArray();
		}


		public static bool[] ByteToBits(byte b)
		{
			bool[] bits = new bool[8];
			for (int i = 0; i < 8; i++)
			{
				bits[i] = (b & (1 << i)) != 0;
			}
			Array.Reverse(bits);
			return bits;
		}

		public static bool IsPowerOfTwo(int number)
		{
			return (number != 0) && ((number & (number - 1)) == 0);
		}


		public static byte[] BitsToBytes(bool[] bits)
		{
			int numBytes = bits.Length / 8;
			if (bits.Length % 8 != 0)
				numBytes++;

			byte[] bytes = new byte[numBytes];

			int byteIndex = 0;
			int bitIndex = 7;

			for (int i = 0; i < bits.Length; i++)
			{
				if (bits[i])
					bytes[byteIndex] |= (byte)(1 << bitIndex);

				bitIndex--;

				if (bitIndex < 0)
				{
					bitIndex = 7;
					byteIndex++;
				}
			}

			return bytes;
		}


		public static bool[] CRC16(bool[] data)
		{
			ushort crc = 0xFFFF;
			ushort polynomial = 0xA001;

			for (int i = 0; i < data.Length; i++)
			{
				crc ^= (ushort)((data[i] ? 1 : 0) << 8);

				for (int j = 0; j < 8; j++)
				{
					if ((crc & 0x8000) != 0)
					{
						crc = (ushort)((crc << 1) ^ polynomial);
					}
					else
					{
						crc <<= 1;
					}
				}
			}

			bool[] crcBits = new bool[16];
			for (int i = 0; i < 16; i++)
			{
				crcBits[i] = ((crc >> i) & 0x01) != 0;
			}

			return crcBits;
		}

		private static bool[] GetCRC16Bits(bool[] data)
		{
			if (data.Length <= 16)
				throw new Exception();
			return data.Skip(data.Length - 16).ToArray();
		}

		private static bool[] RemoveCRC16Bits(bool[] data)
		{
			if (data.Length <= 16)
				throw new Exception();
			return data.Take(data.Length - 16).ToArray();
		}

		private static bool IsCRC16Correct(bool[] crc1, bool[] crc2)
		{
			if (crc1.Length != 16 || crc2.Length != 16)
			{
				throw new Exception();
			}

			return crc1.SequenceEqual(crc2);
		}
	}

	

}

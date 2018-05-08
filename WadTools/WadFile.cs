﻿using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using UnityEngine;

/*
Class for loading and pulling lumps from wad files
*/

namespace WadTools {

	public class DirectoryEntry {
		public int position;
		public int size;
		private string _name;
		public string name {
			get { 
				return _name;
			}
			set {
				//_name = value;
				char[] array = value.ToCharArray();
				_name = "";
				for (int i = 0; i < 8; i++) {
					if ((int) array[i] == 0) {
						break;
					} else {
						_name += value[i];
					} 
				}
			}
		}
	}

	public enum DataType {
		MIDI,
		MUS,
		UNKNOWN
	}

	public class WadFile {

		public string type;
		public int numLumps;
		public int directoryPos;
		public List<DirectoryEntry> directory;
		public byte[] wadData;
		public TextureTable textureTable;

		public DataType DetectType(string name) {
			byte[] lump = GetLump(name);
			if (lump[0] == Convert.ToByte('M') &&
				lump[1] == Convert.ToByte('T') &&
				lump[2] == Convert.ToByte('h') &&
				lump[3] == Convert.ToByte('d')) {
				return DataType.MIDI;
			}

			if (lump[0] == Convert.ToByte('M') &&
				lump[1] == Convert.ToByte('U') &&
				lump[2] == Convert.ToByte('S')) {
				return DataType.MUS;
			}

			return DataType.UNKNOWN;
		}

		public string GetLumpAsText(string name) {
			byte[] data = GetLump(name);
			return Encoding.UTF8.GetString(data);
		}

		public MemoryStream GetLumpAsMemoryStream(string name) {
			byte[] data = GetLump(name);
			return new MemoryStream(data);
		}

		public byte[] GetLump(string name) {
			for (int i = directory.Count - 1; i >= 0; i--) {
				if (directory[i].name == name) return GetLump(directory[i]);
			}
			Debug.LogError("Can't find lump: "+name);
			return null;
		}

		public byte[] GetLump(int index) {
			return GetLump(directory[index]);
		}

		public byte[] GetLump(int start, int length) {
			byte[] output = new byte[length];
			Buffer.BlockCopy(wadData, start, output, 0, length);
			return output;
		}

		public byte[] GetLump(DirectoryEntry entry) {
			return GetLump(entry.position, entry.size);
		}

		// Get all lumps with the same name
		public List<byte[]> GetLumps(string name) {
			List<byte[]> output = new List<byte[]>();
			for (int i = 0; i < directory.Count; i++) {
				if (directory[i].name == name) output.Add(GetLump(i));
			}
			return output;
		}

		public int GetIndex(string name) {
			for (int i = directory.Count - 1; i >= 0; i--) {
				if (directory[i].name == name) return i;
			}
			return -1;
		}

		public bool Contains(string name) {
			for (int i = 0; i < directory.Count; i++) {
				if (directory[i].name == name) return true;
			}
			return false;
		}

		public DirectoryEntry GetEntry(string name) {
			for (int i = directory.Count - 1; i >= 0; i--) {
				if (directory[i].name == name) return directory[i];
			}
			return null;
		}

		public void Merge(string wadPath) {
			Merge(new WadFile(wadPath));
		}

		public void Merge(WadFile wad) {
			numLumps += wad.numLumps;
			
			for (int i = 0; i < wad.directory.Count; i++) {
				wad.directory[i].position += wadData.Length;
			}

			directory.AddRange(wad.directory);

			if (textureTable == null && wad.textureTable != null) {
				textureTable = wad.textureTable;
			}

			if (textureTable != null && wad.textureTable != null) {
				textureTable.Merge(wad.textureTable);
			}

			byte[] newWadData = new byte[wadData.Length + wad.wadData.Length];
			Buffer.BlockCopy(wadData, 0, newWadData, 0, wadData.Length);
			Buffer.BlockCopy(wad.wadData, 0, newWadData, wadData.Length, wad.wadData.Length);
			wadData = newWadData;
		}

		public WadFile(string path) {
			wadData = File.ReadAllBytes(path);

			type = new string(Encoding.ASCII.GetChars(wadData, 0, 4));

			numLumps = (int) BitConverter.ToUInt32(wadData,4);
			directoryPos = (int) BitConverter.ToUInt32(wadData, 8);

			directory = new List<DirectoryEntry>();

			for (int i = directoryPos; i < directoryPos + (numLumps * 16); i += 16) {

				DirectoryEntry de = new DirectoryEntry() {
					position = (int) BitConverter.ToUInt32(wadData, i),
					size = (int) BitConverter.ToUInt32(wadData, i + 4),
					name = new string(Encoding.ASCII.GetChars(wadData, i + 8, 8))
				};

				directory.Add(de);
			}

			if (Contains("PNAMES")) {
				PatchTable pnames = new PatchTable(GetLump("PNAMES"));
				textureTable = new TextureTable(GetLump("TEXTURE1"), pnames);
				if (Contains("TEXTURE2")) {
					textureTable.Add(GetLump("TEXTURE2"), pnames);
				}
			}

		}

		public static string FixString(string input) {
			char[] array = input.ToCharArray();
			string output = "";
			for (int i = 0; i < 8; i++) {
				if ((int) array[i] == 0) break;
				output += input[i];
			}
			return output;
		}

		public static string GetString(byte[] data, int offset, int length = 8) {
			return FixString(new string(Encoding.ASCII.GetChars(data, offset, length)));
		}

		public string GetMD5() {
			byte[] md5 = MD5.Create().ComputeHash(wadData);
			StringBuilder sBuilder = new StringBuilder();
			for (int i = 0; i < md5.Length; i++) {
				sBuilder.Append(md5[i].ToString("x2"));
			}
			return sBuilder.ToString();
		}

		public static string GetMD5(string path) {
			byte[] data = File.ReadAllBytes(path);
			byte[] md5 = MD5.Create().ComputeHash(data);
			StringBuilder sBuilder = new StringBuilder();
			for (int i = 0; i < md5.Length; i++) {
				sBuilder.Append(md5[i].ToString("x2"));
			}
			return sBuilder.ToString();
		}

	}

}

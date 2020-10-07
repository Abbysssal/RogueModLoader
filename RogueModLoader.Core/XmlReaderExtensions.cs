using System.Xml;

namespace RogueModLoaderCore
{
	public static class XmlReaderExtensions
	{
		public static bool ReadNonEmptyElement(this XmlReader reader)
		{
			bool isEmpty = reader.IsEmptyElement;
			reader.ReadStartElement();
			return !isEmpty;
		}

	}
}

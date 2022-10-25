using System;
using System.Text;

public class DefaultEncodingProvider : EncodingProvider
{
	public DefaultEncodingProvider()
	{

	}

	public override Encoding GetEncoding(Int32 codepage)
    {
		return null;
    }

	public override Encoding GetEncoding (string name)
    {
		return null;
    }
}

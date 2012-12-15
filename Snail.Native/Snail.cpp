// This is the main DLL file.

#include "stdafx.h"

#include "SlothNative.h"
namespace SlothNative
{

	
void SlothNative::TestSerialize()
{
	Object ^obj = gcnew String("ABCD");
	unsigned char *data = new unsigned char[4096];
	sizeof(obj);
	//*(void**)data = obj;
}

}
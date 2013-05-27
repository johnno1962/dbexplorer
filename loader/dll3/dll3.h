// dll3.h

#pragma once

extern "C" __declspec(dllexport)
           int loadjava(wchar_t *javadll,char *classpath,
						  char *mainclass,char *port);


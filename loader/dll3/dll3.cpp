
// This is the main DLL file to load JAVA jvm into C# runtime.

#include "stdafx.h"

#include <windows.h>
#include <stdio.h>

#include <iostream>
#include <sstream>
#include <string>

#include "dll3.h"

// point %JAVA_HOME% to a valid jdk
#include "jni.h"

typedef _JNI_IMPORT_OR_EXPORT_ jint 
(JNICALL *JNI_CreateFunc)(JavaVM **pvm, void **penv, void *args);

int loadjava(wchar_t *javadll, char *classpath, char *mainclass, char *port) {
	HINSTANCE hJVM = LoadLibrary(javadll);
	if (hJVM == NULL)
		return -1;

	JNI_CreateFunc JNI_CreateJavaVM2 = (JNI_CreateFunc) GetProcAddress(hJVM, "JNI_CreateJavaVM");  
	if (JNI_CreateJavaVM2 == NULL)
		return -2;

	JavaVMInitArgs vm_args;
	JavaVMOption options[100];

	vm_args.version = JNI_VERSION_1_4;
	vm_args.options = options;
	vm_args.nOptions = 0;
	vm_args.ignoreUnrecognized = JNI_TRUE;

	std::stringstream ss(classpath);
	std::string item;

	while( ss >> item )
		options[vm_args.nOptions++].optionString = strdup( item.c_str() );

	/* Create the Java VM */
	JNIEnv *env;
	JavaVM *jvm;

	jint res = JNI_CreateJavaVM2(&jvm, (void**)&env, &vm_args);
	if (res < 0)
		return -3;

	jclass cls = env->FindClass(mainclass);
	if (cls == NULL)
		return -4;

	jmethodID mid = env->GetStaticMethodID( cls, "main", "([Ljava/lang/String;)V");
	if (mid == NULL)
		return -5;

	jstring jstr = env->NewStringUTF(port);
	if (jstr == NULL)
		return -6;

	jclass stringClass = env->FindClass( "java/lang/String");
	jobjectArray args = env->NewObjectArray( 1, stringClass, jstr);
	if (args == NULL)
		return -7;

	env->CallStaticVoidMethod( cls, mid, args);

	if (env->ExceptionOccurred()) {
		env->ExceptionDescribe();
		jvm->DestroyJavaVM();
		return -8;
	}

	jvm->DestroyJavaVM();
	return 0;
}

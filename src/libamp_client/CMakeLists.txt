add_library(libamp_client client.cxx) 


target_include_directories(libamp_client
	PUBLIC ../include
	PRIVATE ../include/private)

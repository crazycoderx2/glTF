// Copyright (c) 2013, Fabrice Robinet
// All rights reserved.
//
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are met:
//
//  * Redistributions of source code must retain the above copyright
//    notice, this list of conditions and the following disclaimer.
//  * Redistributions in binary form must reproduce the above copyright
//    notice, this list of conditions and the following disclaimer in the
//    documentation and/or other materials provided with the distribution.
//
//  THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
// AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
// IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
// ARE DISCLAIMED. IN NO EVENT SHALL <COPYRIGHT HOLDER> BE LIABLE FOR ANY
// DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
// (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
// LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
// ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF
// THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

#include "GLTF.h"

namespace GLTF
{
    GLTFProfile::~GLTFProfile() {}
    
    void GLTFProfile::setGLenumForString(const std::string& str , unsigned int aGLEnum) {
        this->_glEnumForString[str] = aGLEnum;
    }
    
    unsigned int GLTFProfile::getGLenumForString(const std::string& str) {
        return this->_glEnumForString[str];
    }

    
    /*'SCALAR'
     'VEC2'
     'VEC3'
     'VEC4'
     'MAT2'
     'MAT3'
     'MAT4'
     */
    size_t GLTFProfile::getComponentsCountForType(const std::string& type) {
        static std::map<std::string , unsigned int> countForType;
        if (countForType.empty()) {
            countForType["SCALAR"] = 1;
            countForType["VEC2"] = 2;
            countForType["VEC3"] = 3;
            countForType["VEC4"] = 4;
            countForType["MAT3"] = 9;
            countForType["MAT4"] = 16;
        }
        return countForType[type];
    }
    
    /*
     */

}

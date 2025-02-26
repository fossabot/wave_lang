#pragma once
#include "WaveClass.h"
#include "WaveImage.h"

typedef struct
{
    WaveImage* corlib;
    WaveClass* object_class;
    WaveClass* i1_class;
    WaveClass* i2_class;
    WaveClass* i4_class;
    WaveClass* i8_class;
    WaveClass* void_class;
} WaveCore;

static WaveCore* wave_core = new WaveCore();

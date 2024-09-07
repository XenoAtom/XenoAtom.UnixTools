// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

namespace XenoAtom.UnixTools;

/// <summary>
/// Defines a device id (major/minor)
/// </summary>
/// <param name="Major">This field identifies the driver responsible for managing the device. The range of values for the major number can be large, but typically it fits within 12 bits (for example, up to 4095 in Linux).</param>
/// <param name="Minor">This field identifies the specific device or sub-device handled by the driver associated with the major number. It usually fits within 20 bits, allowing for up to 2^20 (roughly 1 million) device instances or partitions for a particular major number.</param>
public readonly record struct DeviceId(uint Major, uint Minor);
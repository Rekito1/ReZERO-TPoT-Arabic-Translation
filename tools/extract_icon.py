"""Recover the installer icon privately from its PE resources without running it."""
import argparse
import ctypes as ct
from ctypes import wintypes as wt
from pathlib import Path
import struct


def extract_icon(path):
    kernel = ct.WinDLL('kernel32', use_last_error=True)
    kernel.LoadLibraryExW.argtypes = (wt.LPCWSTR, wt.HANDLE, wt.DWORD)
    kernel.LoadLibraryExW.restype = wt.HMODULE
    kernel.FreeLibrary.argtypes = (wt.HMODULE,)
    kernel.FindResourceW.argtypes = (wt.HMODULE, ct.c_void_p, ct.c_void_p)
    kernel.FindResourceW.restype = wt.HRSRC
    kernel.SizeofResource.argtypes = (wt.HMODULE, wt.HRSRC)
    kernel.SizeofResource.restype = wt.DWORD
    kernel.LoadResource.argtypes = (wt.HMODULE, wt.HRSRC)
    kernel.LoadResource.restype = wt.HGLOBAL
    kernel.LockResource.argtypes = (wt.HGLOBAL,)
    kernel.LockResource.restype = ct.c_void_p
    callback_type = ct.WINFUNCTYPE(wt.BOOL, wt.HMODULE, ct.c_void_p, ct.c_void_p, wt.LPARAM)
    kernel.EnumResourceNamesW.argtypes = (wt.HMODULE, ct.c_void_p, callback_type, wt.LPARAM)
    kernel.EnumResourceNamesW.restype = wt.BOOL

    # LOAD_LIBRARY_AS_DATAFILE | LOAD_LIBRARY_AS_IMAGE_RESOURCE.
    module = kernel.LoadLibraryExW(str(path.resolve(strict=True)), None, 0x22)
    if not module:
        raise ct.WinError(ct.get_last_error())

    def resource(name, kind):
        handle = kernel.FindResourceW(module, name, kind)
        if not handle:
            raise ct.WinError(ct.get_last_error())
        data = kernel.LockResource(kernel.LoadResource(module, handle))
        if not data:
            raise OSError('Cannot read resource data')
        return ct.string_at(data, kernel.SizeofResource(module, handle))

    groups = []

    @callback_type
    def collect(_module, _kind, name, _context):
        groups.append(name)
        return True

    try:
        if not kernel.EnumResourceNamesW(module, 14, collect, 0) or len(groups) != 1:
            raise ValueError('Expected one icon group')
        group = resource(groups[0], 14)
        reserved, kind, count = struct.unpack_from('<HHH', group)
        if reserved or kind != 1 or len(group) != 6 + 14 * count:
            raise ValueError('Invalid icon group')
        table = bytearray(group[:6])
        images = bytearray()
        for i in range(count):
            start = 6 + 14 * i
            length, icon_id = struct.unpack_from('<IH', group, start + 8)
            data = resource(icon_id, 3)
            if len(data) != length:
                raise ValueError('Invalid icon image size')
            table.extend(group[start:start + 12])
            table.extend(struct.pack('<I', 6 + 16 * count + len(images)))
            images.extend(data)
        return table + images
    finally:
        kernel.FreeLibrary(module)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('installer', type=Path)
    parser.add_argument('output', type=Path)
    args = parser.parse_args()
    icon = extract_icon(args.installer)
    with args.output.open('xb') as stream:
        stream.write(icon)

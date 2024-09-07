#!/bin/sh
# This script is used to create a cpio archive for testing purposes.
set -eu
#trap 'echo "Error on line $LINENO"; exit 1' ERR
archive_name="cpio_archive_test"
mkdir $archive_name
cd $archive_name
echo "Hello World" > file1.txt
echo "Hello World 2" > file2.txt
mkdir dir1
echo "Hello World 300" > dir1/file3.txt
echo "Hello World 4000" > dir1/file4.txt
ln -s file1.txt softlink_file1.txt
ln -s dir1 softlink_dir1
ln file1.txt hardlink1.txt
sudo mknod null c 1 3 # Create a device file
find . | sort | cpio --reproducible -v -o -H newc --owner 0:0 --renumber-inodes  > ../$archive_name.cpio
cd ..
rm -rf $archive_name

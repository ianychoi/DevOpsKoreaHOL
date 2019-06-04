#!/bin/bash

if [ $# -ne 1 ]; then
	echo "usage: $0 <destination>" >&2
	exit 1
fi

cp -r media $1/ &&
cp -r static/* $1/

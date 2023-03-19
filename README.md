# FileMD5

FileMD5 - утилита командной строки для проверки целостности и неизменности
файлов путем подсчета хеша MD5.
Посчитанный хеш утилита сохраняет в оригинальном файле,
используя специальный отдельный поток NTFS (только для NTFS).
Так же хеши могут храниться в отдельных файлах в папке, задаваемой параметром.
Проверка неизменности файла производится сравнением
нового посчитанного хеша с его ранее сохраненной версией.

FileMD5 is a command-line utility for checking the immutability of a file.
The utility calculates the MD5 hash for the file and stores it
inside the source file in a separate NTFS stream named 'MD5' (for NTFS only).
The immutability of the file is checked by comparing the new hash of the file with
the old saved version of the hash of the file.

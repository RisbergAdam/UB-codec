Resize video:s
./ffmpeg.exe -i mountain_village.mp4 -filter:v scale=640:-1 -c:a copy output.mkv

Split video into frames:
./ffmpeg.exe -i output.mkv -r 10 ./UBCodec/resources/mountain-village-split/frame\_%04d.png

Create video from frames:
./ffmpeg.exe -framerate 30 -i ./UBCodec/resources/mountain-village-split/frame\_%04d.png -c:v libx264 -pix_fmt yuv420p final_video.mp4

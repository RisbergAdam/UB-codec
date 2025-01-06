# - video to images
# ffmpeg -f image2 -framerate 25 -i img_out/%05d.png -vcodec libx264 -crf 0 -qp 0 video.mp4

# - images to video
ffmpeg -f image2 -framerate 25 -i ezgif-6-e3be30c4ce-png-split/ezgif-frame-%03d.png -vcodec libx264 -crf 0 -qp 0 video.mp4
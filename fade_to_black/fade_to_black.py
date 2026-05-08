import cv2
import numpy as np
import matplotlib.pyplot as plt
from matplotlib.widgets import Slider, Button
from matplotlib.patches import Ellipse
import json
import os
import sys
import tkinter as tk
from tkinter import filedialog

# Ensure external window for Spyder
try:
    from IPython import get_ipython
    if get_ipython() is not None:
        get_ipython().run_line_magic('matplotlib', 'qt')
except:
    pass

def get_input_file():
    # 1. Check Command Line
    if len(sys.argv) > 1:
        path = sys.argv[1]
        if os.path.exists(path):
            return path
    
    # 2. If not specified, open File Dialog
    root = tk.Tk()
    root.withdraw() # Hide the main tkinter window
    root.attributes("-topmost", True) # Bring dialog to front
    
    file_path = filedialog.askopenfilename(
        title="Select Moon Image (TIF/PNG/JPG)",
        filetypes=[("Image files", "*.tif *.tiff *.png *.jpg *.jpeg"), ("All files", "*.*")]
    )
    root.destroy()
    return file_path

def interactive_moon_final(image_path, initial_tw=100):
    if not image_path or not os.path.exists(image_path):
        print("No valid file selected. Exiting.")
        return

    # Path and Data Prep
    base_path, ext = os.path.splitext(image_path)
    json_path = f"{base_path}-faded.json"
    out_img_path = f"{base_path}-faded.tif"

    img_bgr = cv2.imread(image_path, cv2.IMREAD_UNCHANGED)
    if img_bgr is None:
        print(f"Error: Could not load {image_path}")
        return

    # Normalize to float32 for high-precision math
    img_float = img_bgr.astype(np.float32) if img_bgr.dtype != np.uint8 else img_bgr.astype(np.float32) * 257.0

    h, w = img_float.shape[:2]
    # UI display image (8-bit for performance)
    img_rgb_disp = (cv2.cvtColor(img_bgr, cv2.COLOR_BGR2RGB) if img_bgr.dtype == np.uint8 
                    else cv2.cvtColor((img_bgr/256).astype(np.uint8), cv2.COLOR_BGR2RGB))
    
    # Initial Parameter Loading
    if os.path.exists(json_path):
        with open(json_path, 'r') as f:
            saved = json.load(f)
            icx, icy, irx, iry, itw = saved['cx'], saved['cy'], saved['rx'], saved['ry'], saved['tw']
    else:
        gray = cv2.cvtColor(img_bgr, cv2.COLOR_BGR2GRAY) if img_bgr.ndim == 3 else img_bgr
        if gray.dtype != np.uint8: gray = (gray / 256).astype(np.uint8)
        circles = cv2.HoughCircles(cv2.medianBlur(gray, 5), cv2.HOUGH_GRADIENT, 1, 100, param1=50, param2=30)
        if circles is not None:
            icx, icy, ir = map(int, circles[0, 0])
            irx = iry = ir
        else:
            icx, icy, irx, iry = w//2, h//2, min(h, w)//4, min(h, w)//4
        itw = initial_tw

    # Setup Figure
    fig, (ax_diag, ax_res) = plt.subplots(1, 2, figsize=(14, 8), sharex=True, sharey=True)
    plt.subplots_adjust(bottom=0.25, top=0.99, left=0.01, right=0.99, wspace=0.01)
    
    display_diag = ax_diag.imshow(img_rgb_disp)
    display_res = ax_res.imshow(img_rgb_disp)
    for a in [ax_diag, ax_res]: a.axis('off')

    patch_outer = Ellipse((icx, icy), irx*2, iry*2, edgecolor='red', facecolor='none', lw=0.8, ls='--')
    patch_inner = Ellipse((icx, icy), irx*2, iry*2, edgecolor='cyan', facecolor='none', lw=0.8, ls='--')
    ax_diag.add_patch(patch_outer); ax_diag.add_patch(patch_inner)

    widgets = {}
    def create_row(label, y_pos, v_min, v_max, v_init):
        ax_s = plt.axes([0.15, y_pos, 0.65, 0.02])
        slider = Slider(ax_s, label, v_min, v_max, valinit=v_init, valfmt='%d')
        ax_bd = plt.axes([0.81, y_pos, 0.02, 0.02]); ax_bu = plt.axes([0.835, y_pos, 0.02, 0.02])
        btn_d, btn_u = Button(ax_bd, '<'), Button(ax_bu, '>')
        val_text = fig.text(0.87, y_pos + 0.005, f"{int(v_init)}", fontsize=10)
        btn_d.on_clicked(lambda e: slider.set_val(max(v_min, slider.val - 1)))
        btn_u.on_clicked(lambda e: slider.set_val(min(v_max, slider.val + 1)))
        return slider, btn_d, btn_u, val_text

    widgets['cx'] = create_row('Center X', 0.18, 0, w, icx)
    widgets['cy'] = create_row('Center Y', 0.15, 0, h, icy)
    widgets['rx'] = create_row('Outer Rx', 0.12, 1, w, irx)
    widgets['ry'] = create_row('Outer Ry', 0.09, 1, h, iry)
    widgets['tw'] = create_row('Fade Width', 0.06, 1, 500, itw)

    ax_save = plt.axes([0.45, 0.015, 0.1, 0.03])
    btn_save = Button(ax_save, 'Save to TIF', color='#d4f1d4')

    def calculate_mask(cx, cy, rx, ry, tw):
        yy, xx = np.ogrid[:h, :w]
        dist = np.sqrt(((xx - cx)**2 / (rx**2)) + ((yy - cy)**2 / (ry**2)))
        inner_norm = (rx - tw) / rx
        t = np.clip((dist - inner_norm) / (1.0 - inner_norm), 0, 1)
        mask = np.where(dist < inner_norm, 1.0, np.cos(t * np.pi / 2)**2)
        return np.where(dist > 1.0, 0.0, mask)

    def update(val):
        params = [widgets[k][0].val for k in ['cx', 'cy', 'rx', 'ry', 'tw']]
        for k in widgets: widgets[k][3].set_text(f"{int(widgets[k][0].val)}")
        mask = calculate_mask(*params)
        display_res.set_data((img_rgb_disp * np.stack([mask]*3, axis=-1)).astype(np.uint8))
        patch_outer.set_center((params[0], params[1]))
        patch_outer.width, patch_outer.height = params[2]*2, params[3]*2
        patch_inner.set_center((params[0], params[1]))
        patch_inner.width, patch_inner.height = max(0, params[2]-params[4])*2, max(0, params[3]-params[4])*2
        fig.canvas.draw_idle()

    def save_data(event):
        p = {k: widgets[k][0].val for k in ['cx', 'cy', 'rx', 'ry', 'tw']}
        with open(json_path, 'w') as f: json.dump(p, f, indent=4)
        mask = calculate_mask(p['cx'], p['cy'], p['rx'], p['ry'], p['tw'])
        final_16 = np.clip(img_float * np.stack([mask]*3, axis=-1), 0, 65535).astype(np.uint16)
        cv2.imwrite(out_img_path, final_16)
        print(f"Exported: {out_img_path}")

    btn_save.on_clicked(save_data)
    for k in widgets: widgets[k][0].on_changed(update)
    update(None); plt.show()

if __name__ == "__main__":
    target_file = get_input_file()
    if target_file:
        interactive_moon_final(target_file)
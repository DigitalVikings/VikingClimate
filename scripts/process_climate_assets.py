import os
import sys
from PIL import Image, ImageFilter
import numpy as np
from scipy import ndimage

def key_black_background(img: Image.Image, low_thresh: int = 12, high_thresh: int = 50) -> Image.Image:
    """
    Keys out solid black background cleanly, un-premultiplying colors
    to prevent dark fringes around semi-transparent edges.
    """
    img = img.convert("RGBA")
    r, g, b, _ = img.split()
    r_bytes = list(r.tobytes())
    g_bytes = list(g.tobytes())
    b_bytes = list(b.tobytes())
    
    alpha_bytes = []
    unmult_r = []
    unmult_g = []
    unmult_b = []
    
    for rv, gv, bv in zip(r_bytes, g_bytes, b_bytes):
        val = max(rv, gv, bv)
        if val <= low_thresh:
            a = 0
        elif val >= high_thresh:
            a = 255
        else:
            t = (val - low_thresh) / float(high_thresh - low_thresh)
            # smoothstep
            t = t * t * (3 - 2 * t)
            a = int(t * 255)
            
        alpha_bytes.append(a)
        
        if a > 0:
            scale = 255.0 / a
            ur = min(255, int(rv * scale))
            ug = min(255, int(gv * scale))
            ub = min(255, int(bv * scale))
        else:
            ur, ug, ub = 0, 0, 0
            
        unmult_r.append(ur)
        unmult_g.append(ug)
        unmult_b.append(ub)
        
    out_img = Image.new("RGBA", img.size)
    r_out = Image.frombytes("L", img.size, bytes(unmult_r))
    g_out = Image.frombytes("L", img.size, bytes(unmult_g))
    b_out = Image.frombytes("L", img.size, bytes(unmult_b))
    a_out = Image.frombytes("L", img.size, bytes(alpha_bytes))
    return Image.merge("RGBA", (r_out, g_out, b_out, a_out))

def process_icon(input_path: str, output_path: str, target_size: int = 128, margin_pct: float = 0.10):
    img = Image.open(input_path)
    keyed = key_black_background(img)
    
    # Get alpha bounding box
    bbox = keyed.getbbox()
    if bbox:
        cropped = keyed.crop(bbox)
    else:
        cropped = keyed
        
    w, h = cropped.size
    max_dim = max(w, h)
    
    # Target size with padding margin
    inner_size = int(target_size * (1.0 - (margin_pct * 2.0)))
    scale = inner_size / float(max_dim)
    new_w = max(1, int(w * scale))
    new_h = max(1, int(h * scale))
    
    resized = cropped.resize((new_w, new_h), Image.Resampling.LANCZOS)
    
    # Place in center of square target canvas
    canvas = Image.new("RGBA", (target_size, target_size), (0, 0, 0, 0))
    paste_x = (target_size - new_w) // 2
    paste_y = (target_size - new_h) // 2
    canvas.paste(resized, (paste_x, paste_y), resized)
    
    os.makedirs(os.path.dirname(output_path), exist_ok=True)
    canvas.save(output_path, "PNG", optimize=True)
    print(f"[Icon] Saved: {output_path} ({target_size}x{target_size})")

def process_ui_sprite(input_path: str, output_path: str, target_width: int = None, target_height: int = None):
    img = Image.open(input_path).convert("RGB")
    arr = np.array(img)
    gray = np.max(arr, axis=2)
    
    # Outer border flood-fill so internal dark wood is never keyed to transparent
    mask_dark = gray < 12
    labeled, _ = ndimage.label(mask_dark)
    border_pixels = np.concatenate([labeled[0, :], labeled[-1, :], labeled[:, 0], labeled[:, -1]])
    border_labels = np.unique(border_pixels)
    border_labels = border_labels[border_labels > 0]
    is_bg = np.isin(labeled, border_labels)
    
    board_mask = (~is_bg).astype(np.uint8) * 255
    board_mask = ndimage.binary_fill_holes(board_mask).astype(np.uint8) * 255
    mask_im = Image.fromarray(board_mask, mode="L").filter(ImageFilter.GaussianBlur(radius=0.7))
    
    keyed = Image.new("RGBA", img.size)
    keyed.paste(img, (0, 0))
    keyed.putalpha(mask_im)
    
    bbox = keyed.getbbox()
    if bbox:
        cropped = keyed.crop(bbox)
    else:
        cropped = keyed
        
    if target_width and target_height:
        resized = cropped.resize((target_width, target_height), Image.Resampling.LANCZOS)
    elif target_width:
        scale = target_width / float(cropped.width)
        resized = cropped.resize((target_width, int(cropped.height * scale)), Image.Resampling.LANCZOS)
    elif target_height:
        scale = target_height / float(cropped.height)
        resized = cropped.resize((int(cropped.width * scale), target_height), Image.Resampling.LANCZOS)
    else:
        resized = cropped
        
    os.makedirs(os.path.dirname(output_path), exist_ok=True)
    resized.save(output_path, "PNG", optimize=True)
    print(f"[UI] Saved: {output_path} ({resized.width}x{resized.height})")

if __name__ == "__main__":
    if len(sys.argv) < 3:
        print("Usage: process_climate_assets.py <mode: icon|ui> <input> <output> [width] [height]")
        sys.exit(1)
        
    mode = sys.argv[1]
    inp = sys.argv[2]
    out = sys.argv[3]
    
    if mode == "icon":
        size = int(sys.argv[4]) if len(sys.argv) > 4 else 128
        process_icon(inp, out, target_size=size)
    elif mode == "ui":
        tw = int(sys.argv[4]) if len(sys.argv) > 4 else None
        th = int(sys.argv[5]) if len(sys.argv) > 5 else None
        process_ui_sprite(inp, out, target_width=tw, target_height=th)

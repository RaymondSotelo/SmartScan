import cv2
import time
import base64

from flask import Flask, jsonify, request
from flask_cors import CORS

import numpy as np
from ultralytics import YOLO


# ============================================================
# FLASK SETUP
# ============================================================

app = Flask(__name__)
CORS(app)


# ============================================================
# LOAD YOLO MODELS
# ============================================================

print("==========================================")
print(" SmartScan AI Scanner")
print(" YOLOv8s - 512 CPU Test")
print("==========================================")

print("Loading OLD model...")

model_old = YOLO(
    r"C:\YOLO\my_model\my_model.pt"
)

print("OLD model loaded.")

print("Loading NEW model...")

model_new = YOLO(
    r"C:\YOLO\my_model_v2\my_model.pt"
)

print("NEW model loaded.")

print("Both models are ready.")


# ============================================================
# SETTINGS
# ============================================================

CONFIDENCE_THRESHOLD = 0.80

IMAGE_SIZE = 512


# ============================================================
# DECODE BASE64
# ============================================================

def decode_base64_image(base64_image):

    try:

        if "," in base64_image:

            base64_image = base64_image.split(
                ",",
                1
            )[1]

        image_bytes = base64.b64decode(
            base64_image
        )

        np_array = np.frombuffer(
            image_bytes,
            dtype=np.uint8
        )

        image = cv2.imdecode(
            np_array,
            cv2.IMREAD_COLOR
        )

        return image

    except Exception as e:

        print(
            "Image decode error:",
            e
        )

        return None


# ============================================================
# RUN ONE YOLO MODEL
# ============================================================

def run_model(
    model,
    image,
    model_name,
    original_width,
    original_height
):

    detections = []


    # --------------------------------------------------------
    # YOLO INFERENCE
    # --------------------------------------------------------

    results = model.predict(
        source=image,
        imgsz=IMAGE_SIZE,
        conf=CONFIDENCE_THRESHOLD,
        device="cpu",
        verbose=False
    )


    # --------------------------------------------------------
    # YOLO RESULT
    # --------------------------------------------------------

    for result in results:

        for box in result.boxes:

            confidence = float(
                box.conf[0]
            )

            if confidence < CONFIDENCE_THRESHOLD:
                continue


            class_id = int(
                box.cls[0]
            )

            name = model.names[
                class_id
            ]


            # ------------------------------------------------
            # YOLO gives coordinates relative to the image
            # actually passed into YOLO.
            # ------------------------------------------------

            x1, y1, x2, y2 = map(
                int,
                box.xyxy[0].tolist()
            )


            # ------------------------------------------------
            # IMPORTANT:
            #
            # We resize the original image to 512x288 before
            # sending it to YOLO.
            #
            # Therefore convert the coordinates back to
            # the original 1280x720 image.
            # ------------------------------------------------

            processed_height, processed_width = (
                image.shape[:2]
            )


            scale_x = (
                original_width /
                processed_width
            )

            scale_y = (
                original_height /
                processed_height
            )


            x1 = int(
                x1 * scale_x
            )

            y1 = int(
                y1 * scale_y
            )

            x2 = int(
                x2 * scale_x
            )

            y2 = int(
                y2 * scale_y
            )


            # ------------------------------------------------
            # LIMIT BOX TO ORIGINAL IMAGE
            # ------------------------------------------------

            x1 = max(
                0,
                min(
                    x1,
                    original_width
                )
            )

            y1 = max(
                0,
                min(
                    y1,
                    original_height
                )
            )

            x2 = max(
                0,
                min(
                    x2,
                    original_width
                )
            )

            y2 = max(
                0,
                min(
                    y2,
                    original_height
                )
            )


            print(
                f"{model_name}: "
                f"{name} "
                f"{confidence * 100:.1f}% "
                f"BOX=({x1},{y1},{x2},{y2})"
            )


            detections.append({

                "name": name,

                "confidence": round(
                    confidence * 100,
                    1
                ),

                "is_confident": True,

                "box": [
                    x1,
                    y1,
                    x2,
                    y2
                ]

            })


    return detections


# ============================================================
# DETECT FRAME
# ============================================================

@app.route(
    "/detect_frame",
    methods=["POST"]
)
def detect_frame():

    total_start = time.perf_counter()


    try:

        # ====================================================
        # RECEIVE JSON
        # ====================================================

        data = request.get_json(
            silent=True
        )


        if data is None:

            return jsonify({
                "error": "Invalid JSON"
            }), 400


        base64_image = data.get(
            "image"
        )


        if not base64_image:

            return jsonify({
                "error": "No image provided"
            }), 400


        # ====================================================
        # DECODE
        # ====================================================

        decode_start = time.perf_counter()

        img = decode_base64_image(
            base64_image
        )


        decode_time = (
            time.perf_counter()
            - decode_start
        )


        if img is None:

            return jsonify({
                "error": "Could not decode image"
            }), 400


        # ====================================================
        # ORIGINAL IMAGE SIZE
        # ====================================================

        original_height, original_width = (
            img.shape[:2]
        )


        print("")
        print("==========================================")
        print("NEW FRAME")

        print(
            f"Original image: "
            f"{original_width}x{original_height}"
        )


        # ====================================================
        # RESIZE FOR YOLO
        # ====================================================

        resize_start = time.perf_counter()


        if original_width > IMAGE_SIZE:

            scale = (
                IMAGE_SIZE /
                original_width
            )

            new_width = IMAGE_SIZE

            new_height = int(
                original_height * scale
            )


            img_for_yolo = cv2.resize(
                img,
                (
                    new_width,
                    new_height
                ),
                interpolation=cv2.INTER_AREA
            )

        else:

            img_for_yolo = img.copy()


        resize_time = (
            time.perf_counter()
            - resize_start
        )


        print(
            f"YOLO image: "
            f"{img_for_yolo.shape[1]}x"
            f"{img_for_yolo.shape[0]}"
        )


        # ====================================================
        # OLD MODEL
        # ====================================================

        old_start = time.perf_counter()


        old_results = run_model(
            model_old,
            img_for_yolo,
            "OLD",
            original_width,
            original_height
        )


        old_time = (
            time.perf_counter()
            - old_start
        )


        # ====================================================
        # NEW MODEL
        # ====================================================

        new_start = time.perf_counter()


        new_results = run_model(
            model_new,
            img_for_yolo,
            "NEW",
            original_width,
            original_height
        )


        new_time = (
            time.perf_counter()
            - new_start
        )


        # ====================================================
        # COMBINE
        # ====================================================

        detections = (
            old_results +
            new_results
        )


        # ====================================================
        # TOTAL TIME
        # ====================================================

        total_time = (
            time.perf_counter()
            - total_start
        )


        print("")
        print("------------- PERFORMANCE ---------------")

        print(
            f"Image decode:     "
            f"{decode_time:.4f} sec"
        )

        print(
            f"Image resize:     "
            f"{resize_time:.4f} sec"
        )

        print(
            f"OLD YOLO:         "
            f"{old_time:.4f} sec"
        )

        print(
            f"NEW YOLO:         "
            f"{new_time:.4f} sec"
        )

        print(
            f"TOTAL YOLO:       "
            f"{old_time + new_time:.4f} sec"
        )

        print(
            f"TOTAL REQUEST:    "
            f"{total_time:.4f} sec"
        )

        print(
            f"OLD detections:   "
            f"{len(old_results)}"
        )

        print(
            f"NEW detections:   "
            f"{len(new_results)}"
        )

        print("------------------------------------------")


        # ====================================================
        # RETURN
        # ====================================================

        return jsonify({

            "detections": detections,

            "processing_time": round(
                total_time,
                4
            )

        })


    except Exception as e:

        print("")
        print("!!!!!!!!!!!! ERROR !!!!!!!!!!!!")
        print(
            str(e)
        )
        print("!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!")


        return jsonify({
            "error": str(e)
        }), 500


# ============================================================
# CONNECTION TEST
# ============================================================

@app.route(
    "/",
    methods=["GET"]
)
def home():

    return jsonify({
        "status": "SmartScan AI Scanner is running"
    })


# ============================================================
# START SERVER
# ============================================================

if __name__ == "__main__":

    print("")
    print("==========================================")
    print(" SmartScan AI Scanner")
    print(" YOLOv8s x2")
    print(" 512 CPU Mode")
    print(" Port 5000")
    print("==========================================")
    print("")


    app.run(
        host="0.0.0.0",
        port=5000,
        debug=False,
        threaded=True
    )
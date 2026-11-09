#!/usr/bin/env python3
"""
animal_id.py - guess which animal is in a picture.

Uses OpenAI's CLIP model (zero-shot), so no training is needed and you can
change the list of animals freely. :3

Usage:
    python animal_id.py photo.jpg
    python animal_id.py photo.jpg --top 5
    python animal_id.py photo.jpg --threshold 0.8
    python animal_id.py photo.jpg --labels "lion,tiger,house cat,dog"
"""

import argparse
import sys

import torch
from PIL import Image
from transformers import CLIPModel, CLIPProcessor

DEFAULT_ANIMALS = [
    "lion", "tiger", "leopard", "cheetah", "jaguar", "wolf", "fox", "bear",
    "polar bear", "panda", "elephant", "giraffe", "zebra", "rhinoceros",
    "hippopotamus", "monkey", "gorilla", "kangaroo", "koala", "deer", "moose",
    "dog", "cat", "horse", "cow", "pig", "sheep", "goat", "rabbit", "hamster",
    "squirrel", "mouse", "eagle", "owl", "parrot", "penguin", "flamingo",
    "duck", "chicken", "swan", "pigeon", "crow", "snake", "lizard", "turtle",
    "crocodile", "frog", "fish", "shark", "dolphin", "whale", "octopus",
    "butterfly", "bee", "spider",
]


def article(word: str) -> str:
    return "an" if word[0].lower() in "aeiou" else "a"


def main() -> None:
    p = argparse.ArgumentParser(description="Identify the animal in an image.")
    p.add_argument("image", help="path to the image file")
    p.add_argument("-k", "--top", type=int, default=3,
                   help="how many guesses to show when uncertain (default 3)")
    p.add_argument("-t", "--threshold", type=float, default=0.6,
                   help="confidence (0-1) needed to give a single answer (default 0.6)")
    p.add_argument("--labels", help="comma-separated list of animals to choose from")
    p.add_argument("--model", default="openai/clip-vit-base-patch32",
                   help="CLIP model from Hugging Face (larger: openai/clip-vit-large-patch14)")
    args = p.parse_args()

    labels = ([s.strip() for s in args.labels.split(",") if s.strip()]
              if args.labels else DEFAULT_ANIMALS)

    try:
        image = Image.open(args.image).convert("RGB")
    except Exception as e:
        sys.exit(f"Could not open image: {e}")

    device = "cuda" if torch.cuda.is_available() else "cpu"
    model = CLIPModel.from_pretrained(args.model).to(device).eval()
    processor = CLIPProcessor.from_pretrained(args.model)

    prompts = [f"a photo of {article(a)} {a}" for a in labels]
    inputs = processor(text=prompts, images=image,
                       return_tensors="pt", padding=True).to(device)

    with torch.no_grad():
        probs = model(**inputs).logits_per_image.softmax(dim=-1)[0].cpu()

    k = min(args.top, len(labels))
    values, indices = probs.topk(k)
    best_p = values[0].item()

    if best_p >= args.threshold:
        print(labels[indices[0]])
    else:
        print("Not sure - best guesses:")
        width = max(len(labels[i]) for i in indices)
        for v, i in zip(values, indices):
            print(f"  {labels[i]:<{width}}  {v.item() * 100:5.1f}%")


if __name__ == "__main__":
    main()

use <cutouts/screws.scad>

$fn = $preview ? 36 : 360;

// Lenght of tablet
l_tablet = 240;

// Width of tablet
w_tablet = 150;

// Thickness of tablet
t_tablet = 5.5;

// Bezel width
bezel = 13;

// Bezel length at the switch
switch = 25;

// Edge width
edge = 10;

// Length of diffuser
l = l_tablet - switch + edge;

// Width of diffuser
w = w_tablet + 2*edge;

// Floor thickness
t_floor = 0.6;

// Corner radius
R = 5;

// Reflector thickness
t_refl = 1;

// Diffuser thickness
t_diff = 4;

// Rail width
w_rail = 1;

// Positioner thickness
t_pos = 1;

// Tolerance for reflector and diffuser
tol = 0.25;

// Bolt M
M = 4;

// Bolt offset
offset = 1;

// Scope outer diameter
D_scope_outer = 84;

// Scope inner diameter
D_scope_inner = 80;

// Padding thickness (compressed)
t_padding = 0.5;

// Height of tube
h_tube = 50;

// Tube thickness
t_tube = 4*0.45;

// Calculate total thickness
t_total = t_floor + t_tablet + bezel + t_refl + t_diff + t_pos;
echo(str("l:", l, ", w: ", w, ", total thickness: ", t_total));

// Bolts and nuts
module nb(){
  for( x = [R + offset, l/2, l - R - offset], y = [R + offset, w - R - offset] ){
    translate([x, y, bezel + t_refl + t_diff + t_pos]) clamp_7380_2_934(M=M, gap=t_total, bolt_centered=true, bolt_length=25, depth_tol=0, nut_tol=0, extra_nut_length=t_total - 25 + 0.01);
  }
}
*nb();

// Lower part
module lower(){
  difference(){
    translate([0, 0, -t_tablet])
    difference(){
      translate([0, 0, -t_floor]) linear_extrude(t_floor + t_tablet){
        hull(){
          translate([R, R]) circle(r=R);
          translate([l - R, R]) circle(r=R);
          translate([R, w - R]) circle(r=R);
          translate([l - R, w - R]) circle(r=R);
        }
      }
      translate([2*edge, 2*edge, -t_floor - 0.01]) cube([l - 4*edge, w - 4*edge, t_floor + 0.02]);
      translate([edge, edge, 0]) cube([l - edge + 0.01, w - 2*edge, t_tablet + 0.01]);
    }
    nb();
  }
}
*lower();

// Middle part
module middle(){
  difference(){
    linear_extrude(bezel + t_refl + t_diff + t_pos){
      hull(){
        translate([R, R]) circle(r=R);
        translate([l - R, R]) circle(r=R);
        translate([R, w - R]) circle(r=R);
        translate([l - R, w - R]) circle(r=R);
      }
    }
    hull(){
      translate([edge + bezel, edge + bezel, -0.01]) cube([l - 2*edge - 2*bezel, w - 2*edge - 2*bezel, 0.01]);
      translate([edge, edge, bezel]) cube([l - 2*edge + 0.01, w - 2*edge, t_refl + t_diff + 0.01]);
    }
    translate([0, 0, bezel]) linear_extrude(t_refl + t_diff + t_pos + 0.01){
      RR = R - w_rail;
      hull(){
        translate([R, R]) circle(r=RR);
        translate([l - R, R]) circle(r=RR);
        translate([R, w - R]) circle(r=RR);
        translate([l - R, w - R]) circle(r=RR);
      }
    }
    nb();
  }
}
*middle();

// Reflector
module reflector(){
  difference(){
    union(){
      translate([0, 0, bezel]) linear_extrude(t_refl){
        RR = R - w_rail - tol;
        difference(){
          hull(){
            translate([R, R]) circle(r=RR);
            translate([l - R, R]) circle(r=RR);
            translate([R, w - R]) circle(r=RR);
            translate([l - R, w - R]) circle(r=RR);
          }
          translate([edge + t_refl, edge + t_refl]) square([l - 2*edge - 2*t_refl, w - 2*edge - 2*t_refl]);
        }
      }
      difference(){
        hull(){
          translate([edge + bezel, edge + bezel, 0]) cube([l - 2*edge - 2*bezel, w - 2*edge - 2*bezel, 0.01]);
          translate([edge, edge, bezel - 0.01]) cube([l - 2*edge, w - 2*edge, 0.01]);
        }
        hull(){
          translate([edge + bezel + t_refl, edge + bezel + t_refl, -0.01]) cube([l - 2*edge - 2*bezel - 2*t_refl, w - 2*edge - 2*bezel - 2*t_refl, 0.01]);
          translate([edge + t_refl, edge + t_refl, bezel]) cube([l - 2*edge - 2*t_refl, w - 2*edge - 2*t_refl, 0.01]);
        }
      }
    }
    nb();
  }
}
*reflector();

// Diffuser
module diffuser(){
  difference(){
    translate([0, 0, bezel + t_refl]) linear_extrude(t_diff){
      RR = R - w_rail - tol;
      difference(){
        hull(){
          translate([R, R]) circle(r=RR);
          translate([l - R, R]) circle(r=RR);
          translate([R, w - R]) circle(r=RR);
          translate([l - R, w - R]) circle(r=RR);
        }
      }
    }
    nb();
  }
}
*diffuser();

// Positioner
module positioner(){
  difference(){
    translate([0, 0, bezel + t_refl + t_diff]){
      difference(){
        union(){
          linear_extrude(t_pos){
            RR = R - w_rail - tol;
            hull(){
              translate([R, R]) circle(r=RR);
              translate([l - R, R]) circle(r=RR);
              translate([R, w - R]) circle(r=RR);
              translate([l - R, w - R]) circle(r=RR);
            }
          }
          translate([l/2, w/2, t_pos]) cylinder(d=D_scope_outer + 2*t_padding + 2*t_tube, h=h_tube);
        }
        translate([l/2, w/2, t_pos]) cylinder(d=D_scope_outer + 2*t_padding, h=h_tube + 0.01);
        translate([l/2, w/2, -0.01]) cylinder(d=D_scope_inner, h=t_pos + 0.02);
      }
    }
    nb();
  }
}
*positioner();

///////////////////////////////////////////////////////////////////////////////////////
// What to print
// 0: all; 1: lower; 2: upper; 3: reflector;
// 4: diffuser; 5: diffuser projection; 6: positioner
///////////////////////////////////////////////////////////////////////////////////////

what = 4
;

if( what == 0 ){
  #nb();
  lower();
  middle();
  reflector();
  %diffuser();
  %positioner();
}
if( what == 1 ){
  lower();
}
if( what == 2 ){
  middle();
}
if( what == 3 ){
  reflector();
}
if( what == 4 ){
  diffuser();
}
if( what == 5 ){
  projection([0, 0, 1]) diffuser();
}
if( what == 6 ){
  positioner();
}

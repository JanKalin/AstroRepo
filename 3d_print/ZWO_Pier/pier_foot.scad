$fn = $preview ? 120 : 720;
use <threads-scad-master/threads.scad>

// Plate thickness
t_plate = 10;

// Pier height
h_pier = 190;

// Pier diameter
D_pier = 124;
echo(str("D_pier = ", D_pier));

// Diameter of pier hole locations
D_pier_hole = 100.5;
echo(str("D_pier_hole = ", D_pier_hole));

// Diameter of pier hole bolts (plus tolerance)
M_pier_hole = 6 + 0.25;
echo(str("M_pier_hole = ", M_pier_hole));

// Diameter of pier hole heads
D_pier_hole_head = 9;

// Depth of pier hole heads
d_pier_hole_head = 8;

// Rail widths
w_rail = 140;

// Rail heights
h_rail = 60;

// Diameter of rail bolts
M_rail_bolts = 8;

// Diameter of rail hole
D_rail_hole = M_rail_bolts + 0.5;
echo(str("D_rail_hole = ", D_rail_hole));

// Diameter of rail washers
D_rail_washers = [8.4, 16];

// Thickness of rail washers
t_rail_washers = 1.6;

// Diameter of rail nuts
D_nuts = 13/cos(30);

// Diameter of rail nut support material
D_support = D_nuts + t_plate;
echo(str("D_support = ", D_support));

// Spanner ring thickness
t_spanner = 4;

// Generate four rail bolts
four_bolts = false;

// DEBUG: show nuts, bolts,...
debug = true;

// Rail bolt holes
rail_hole_initial_angle = 225;
rail_hole_delta_angle = asin((w_rail/6)/(D_pier/2 + D_nuts/2 + t_spanner));
rail_hole_angles = four_bolts ? [-rail_hole_delta_angle, rail_hole_delta_angle, 180 - rail_hole_delta_angle, 180 + rail_hole_delta_angle] : [ for( a = [0, 120, 240] ) a + rail_hole_initial_angle ];
rail_holes = [ for( a = rail_hole_angles ) [cos(a)*(D_pier/2 + D_nuts/2 + t_spanner), sin(a)*(D_pier/2 + D_nuts/2 + t_spanner)] ];
echo(str("2*(D_pier/2 + D_nuts/2 + t_spanner) = ", 2*(D_pier/2 + D_nuts/2 + t_spanner)));

// Pier bolt holes
pier_hole_initial_angle = 225;
pier_hole_angles = [ for( a = [0, 120, 240] ) a + pier_hole_initial_angle ];
pier_holes = [ for( a = pier_hole_angles ) [cos(a)*D_pier_hole/2, sin(a )*D_pier_hole/2]];

/////////////////////////////////////////////////////////////////////////////////////////
// Basic shape
/////////////////////////////////////////////////////////////////////////////////////////

module basic_shape(holes=true, expand=0){
  difference(){
    hull(){
      circle(d=D_pier + expand);
      for( rail_hole = rail_holes ){
        translate(rail_hole) circle(d=D_support + expand);
      }
    }
    if( holes ){
      for( rail_hole = rail_holes ){
        translate(rail_hole) circle(d=D_rail_hole);
      }
      for( pier_hole = pier_holes ){
        translate(pier_hole) circle(d=M_pier_hole);
      }
    }
  }
}

module final_shape(flat=false){
  difference(){
    linear_extrude(t_plate) basic_shape();
    if( !flat ){
      for( pier_hole = pier_holes ){
        translate([pier_hole[0], pier_hole[1], t_plate - d_pier_hole_head]) cylinder(d=D_pier_hole_head, h=d_pier_hole_head + 0.01);
      }
    }
  }    
}

module capnut(){
  translate([0, 0, 15 - 12.5/2]) sphere(d=12.5);
  cylinder(d=12.5, h=15 - 12.5/2 + 0.01);
  cylinder(d=13/cos(30), h=6, $fn=6);
}

/////////////////////////////////////////////////////////////////////////////////////////
// 1: 2D shape, 2: 3D shape, 3: 3D shape + debug
// 4: Barrier for grouting, 5: Bole locator, 6: thread protector
/////////////////////////////////////////////////////////////////////////////////////////

what = 3;

if( what == 1 ){
  basic_shape();
}
if( what == 2 ){
  final_shape();
}
if( what == 3 ){
//  translate([15, -20, 0])
//  translate([w_rail, w_rail, 0])
//  rotate([0, 0, -15])
//  translate([-w_rail, -w_rail, 0])
  translate([w_rail - D_pier/2/sqrt(2), w_rail - D_pier/2/sqrt(2), 0]){
    color("DarkSlateGray") final_shape(flat=true);
    if( $preview ){
      %translate([0, 0, t_plate + 0.01]) cylinder(d=D_pier, h=h_pier);
    }
    else{
      color("LightGrey") translate([0, 0, t_plate + 0.01]) cylinder(d=D_pier, h=h_pier);
    }
    %for( rail_hole = rail_holes ){
      translate([rail_hole[0], rail_hole[1], t_plate]){
        difference(){
          cylinder(d=D_rail_washers[1], h=t_rail_washers);
          translate([0, 0, -0.01]) cylinder(d=D_rail_washers[0], h=t_rail_washers + 0.02);
        }
        translate([0, 0, t_rail_washers + 0.01]) capnut();
      }
    }
  }
  color("yellow") translate([0, 0, -h_rail]) cube([300, w_rail, h_rail]);
  color("yellow") translate([0, 0, -h_rail]) cube([w_rail, 300, h_rail]);
  color("yellow") translate([0, 0, -300]) cube([w_rail - 50, w_rail - 50, 300]);
}
if( what == 4 ){
  linear_extrude(t_plate)
  difference(){
    basic_shape(expand=5);
    basic_shape(expand=0.5);
  }
}
if( what == 5 ){
  difference(){
    union(){
      linear_extrude(t_plate)
      for( a = rail_holes, b = rail_holes ){
        if( a == b ){
          translate(a) circle(d=M_rail_bolts + 4);
        }
        else{
          hull(){
            translate(a) circle(d=4);
            translate(b) circle(d=4);
          }
        }
      }
      for( a = rail_holes ){
        translate(a) cylinder(d=M_rail_bolts + 6, h=t_plate + 1.6 + 2);
      }
    }
    for( a = rail_holes ){
      translate(a) translate([0, 0, -0.01]) ScrewThread(M_rail_bolts, t_plate + 1.6 + 2 + 0.02);
    }
  }
}